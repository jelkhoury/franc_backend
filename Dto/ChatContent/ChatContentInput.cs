namespace FrancProject.Dto.ChatContent;

public sealed class ChatContentInput
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string TimeZone { get; set; } = "Asia/Beirut";
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string Status { get; set; } = "draft";
    public bool IsCancelled { get; set; }
    public int? EventId { get; set; }
    public int? Version { get; set; }
}
public sealed record ChatContentView(int Id, string Type, string Name, string Description,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, string TimeZone, string? Location,
    string? Url, string Status, bool IsCancelled, int? EventId, int Version,
    string EmbeddingStatus, string Availability);
