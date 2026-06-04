namespace FrancProject.Dto;

public class JobPostResponseDto
{
    public string LinkedinUrl { get; set; } = null!;
    public string? SourceJobId { get; set; }
    public string Title { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? LocationText { get; set; }
    public DateTime? DatePosted { get; set; }
    public string? PostedTimeAgo { get; set; }
    public bool IsRemote { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? DetailsFetchedAt { get; set; }
}
