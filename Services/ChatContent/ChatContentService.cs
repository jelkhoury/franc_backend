using FrancProject.Data;
using FrancProject.Dto.ChatContent;
using FrancProject.Models.ChatContent;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services.ChatContent;

public sealed class ChatContentService(DataContext db)
{
    public static ChatContentView View(IChatContent e, string type) => new(e.Id, type, e.Name, e.Description,
        e.StartsAt, e.EndsAt, e.TimeZone, e.Location, e.Url, e.Status, e.IsCancelled,
        (e as ChatSurvey)?.EventId, e.Version, e.EmbeddingStatus,
        ChatContentPolicy.Availability(type, e.IsCancelled, e.StartsAt, e.EndsAt, DateTimeOffset.UtcNow));

    public async Task<ChatContentView?> Find(string type, int id, bool admin, CancellationToken ct)
    {
        var rows = await List(type, admin, [id], null, null, null, null, true, 1, 1, ct);
        return rows.Items.FirstOrDefault();
    }
    public sealed record Page(List<ChatContentView> Items, int Total, int PageNumber, int PageSize);
    public Task<Page> List(string type, bool admin, int[]? ids, DateTimeOffset? from, DateTimeOffset? to,
        int? eventId, string? query, bool includeUnavailable, int page, int pageSize, CancellationToken ct, int[]? eventIds = null) => type == "event"
        ? ListCore<ChatEvent>(type, admin, ids, from, to, eventId, query, includeUnavailable, page, pageSize, ct, eventIds)
        : ListCore<ChatSurvey>(type, admin, ids, from, to, eventId, query, includeUnavailable, page, pageSize, ct, eventIds);

    private async Task<Page> ListCore<T>(string type, bool admin, int[]? ids, DateTimeOffset? from, DateTimeOffset? to,
        int? eventId, string? text, bool includeUnavailable, int page, int pageSize, CancellationToken ct, int[]? eventIds) where T : class, IChatContent
    {
        var q = BuildQuery<T>(type, admin, ids, from, to, eventId, text, includeUnavailable, eventIds);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(x => View(x, type)).ToList(), total, page, pageSize);
    }

    internal IQueryable<T> BuildQuery<T>(string type, bool admin, int[]? ids, DateTimeOffset? from,
        DateTimeOffset? to, int? eventId, string? text, bool includeUnavailable, int[]? eventIds = null) where T : class, IChatContent
    {
        IQueryable<T> q = db.Set<T>().AsNoTracking();
        if (!admin) q = q.Where(x => x.Status == "published");
        if (typeof(T) == typeof(ChatSurvey) && !admin)
            q = q.Where(x => EF.Property<int?>(x, "EventId") == null || db.ChatEvents.Any(e => e.Id == EF.Property<int?>(x, "EventId") && e.Status == "published"));
        if (ids?.Length > 0) q = q.Where(x => ids.Contains(x.Id));
        if (from.HasValue) { var value = from.Value.ToUniversalTime(); q = q.Where(x => (x.EndsAt ?? x.StartsAt) >= value || (type == "survey" && x.EndsAt == null)); }
        if (to.HasValue) { var value = to.Value.ToUniversalTime(); q = q.Where(x => x.StartsAt < value || (type == "survey" && x.StartsAt == null)); }
        if (eventId.HasValue && typeof(T) == typeof(ChatSurvey)) q = q.Where(x => EF.Property<int?>(x, "EventId") == eventId);
        if (eventIds?.Length > 0 && typeof(T) == typeof(ChatSurvey)) q = q.Where(x => eventIds.Contains(EF.Property<int?>(x, "EventId") ?? -1));
        if (!string.IsNullOrWhiteSpace(text)) { var term = text.Trim().ToLower(); q = q.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term)); }
        if (!includeUnavailable) { var now = DateTimeOffset.UtcNow; q = q.Where(x => !x.IsCancelled && (x.EndsAt == null ? (type == "survey" || x.StartsAt >= now) : x.EndsAt > now) && (type == "event" || x.StartsAt == null || x.StartsAt <= now)); }
        return q;
    }

    public Task<ChatContentView> Save(string type, int? id, ChatContentInput input, CancellationToken ct) => type == "event"
        ? SaveCore<ChatEvent>(type, id, input, ct) : SaveCore<ChatSurvey>(type, id, input, ct);
    private async Task<ChatContentView> SaveCore<T>(string type, int? id, ChatContentInput input, CancellationToken ct) where T : class, IChatContent, new()
    {
        var errors = ChatContentPolicy.Validate(type, input);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        if (type == "survey" && input.EventId.HasValue && !await db.ChatEvents.AnyAsync(x => x.Id == input.EventId, ct)) throw new ArgumentException("Event does not exist.");
        var entity = id.HasValue ? await db.Set<T>().FindAsync([id.Value], ct) ?? throw new KeyNotFoundException() : new T();
        if (id.HasValue && input.Version != entity.Version) throw new DbUpdateConcurrencyException();
        entity.Name = input.Name.Trim(); entity.Description = input.Description.Trim();
        entity.StartsAt = input.StartsAt?.ToUniversalTime(); entity.EndsAt = input.EndsAt?.ToUniversalTime();
        entity.TimeZone = input.TimeZone; entity.Location = input.Location?.Trim(); entity.Url = string.IsNullOrWhiteSpace(input.Url) ? null : input.Url.Trim();
        entity.Status = input.Status; entity.IsCancelled = input.IsCancelled; entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.EmbeddingStatus = "pending";
        if (entity is ChatSurvey survey) survey.EventId = input.EventId;
        if (id.HasValue) entity.Version++; else db.Set<T>().Add(entity);
        db.ChatContentIndexJobs.Add(new() { Type = type, Event = entity as ChatEvent,
            Survey = entity as ChatSurvey, Version = entity.Version });
        // EF inserts the record and its job in one transaction, fixing up generated foreign keys.
        await db.SaveChangesAsync(ct);
        return View(entity, type);
    }
    public async Task Retry(string type, int id, CancellationToken ct)
    {
        IChatContent entity = type == "event"
            ? await db.ChatEvents.FindAsync([id], ct) ?? throw new KeyNotFoundException()
            : await db.ChatSurveys.FindAsync([id], ct) ?? throw new KeyNotFoundException();
        entity.EmbeddingStatus = "pending";
        db.ChatContentIndexJobs.Add(new() { Type = type, Event = entity as ChatEvent, Survey = entity as ChatSurvey, Version = entity.Version });
        await db.SaveChangesAsync(ct);
    }
}
