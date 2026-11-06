using System.Net.Http.Json;
using FrancProject.Data;
using FrancProject.Models.ChatContent;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services.ChatContent;

public sealed class ChatContentIndexWorker(IServiceScopeFactory scopes, IHttpClientFactory clients,
    IConfiguration config, ILogger<ChatContentIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = config["ChatContent:FlaskBaseUrl"]?.TrimEnd('/');
        var key = config["ChatContent:IndexKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) {
            logger.LogWarning("Chat content indexing disabled: configure ChatContent:FlaskBaseUrl and ChatContent:IndexKey.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessNext(url, key, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Chat content index worker failed ({ErrorType}).", e.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
    private async Task ProcessNext(string baseUrl, string key, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var now = DateTimeOffset.UtcNow;
        var job = await db.ChatContentIndexJobs.Where(x => x.CompletedAt == null && x.Attempts < 5 && x.NextAttemptAt <= now).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (job == null) return;
        IChatContent? record = job.Type == "event"
            ? await db.ChatEvents.FindAsync([job.RecordId], ct)
            : await db.ChatSurveys.FindAsync([job.RecordId], ct);
        if (record == null || record.Version != job.Version) { job.CompletedAt = now; await db.SaveChangesAsync(ct); return; }
        job.Attempts++;
        try {
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/internal/chat-content/index");
            request.Headers.Add("X-Chat-Content-Key", key);
            request.Content = JsonContent.Create(ChatContentService.View(record, job.Type));
            using var response = await clients.CreateClient("ChatContentIndexer").SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            job.CompletedAt = DateTimeOffset.UtcNow;
            record.EmbeddingStatus = "ready";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) {
            record.EmbeddingStatus = "failed";
            job.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 10 * Math.Pow(2, job.Attempts)));
            logger.LogWarning("Indexing {Type} {RecordId} failed on attempt {Attempt} ({ErrorType}).", job.Type, job.RecordId, job.Attempts, e.GetType().Name);
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* An admin edited the record; their newer job owns the status. */ }
    }
}
