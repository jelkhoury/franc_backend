namespace FrancProject.Models.ChatContent;
public sealed class ChatContentIndexJob
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public int? EventId { get; set; }
    public ChatEvent? Event { get; set; }
    public int? SurveyId { get; set; }
    public ChatSurvey? Survey { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int RecordId => EventId ?? SurveyId ?? 0;
    public int Version { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
