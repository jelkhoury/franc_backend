namespace FrancProject.Models.ChatContent;
public interface IChatContent
{

    int Id { get; set; }
    string Name { get; set; }
    string Description { get; set; }
    DateTimeOffset? StartsAt { get; set; }
    DateTimeOffset? EndsAt { get; set; }
    string TimeZone { get; set; }
    string? Location { get; set; }
    string? Url { get; set; }
    string Status { get; set; }
    bool IsCancelled { get; set; }
    int Version { get; set; }
    string EmbeddingStatus { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
