namespace FrancProject.Models.ChatContent;

public sealed class ChatEvent : IChatContent
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string TimeZone { get; set; } = "Asia/Beirut";
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string Status { get; set; } = "draft";
    public bool IsCancelled { get; set; }
    public int Version { get; set; } = 1;
    public string EmbeddingStatus { get; set; } = "pending";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
