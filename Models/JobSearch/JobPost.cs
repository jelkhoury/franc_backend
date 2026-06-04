namespace FrancProject.Models
{
    public class JobPost
    {
        public long Id { get; set; }

        public string LinkedinUrl { get; set; } = null!;
        public string? SourceJobId { get; set; }

        public string Title { get; set; } = null!;
        public string? CompanyName { get; set; }
        public string? LocationText { get; set; }

        public DateTime? DatePosted { get; set; }
        public string? PostedTimeAgo { get; set; }

        public bool IsRemote { get; set; } = false;

        public string? Description { get; set; }
        public DateTimeOffset? DetailsFetchedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<JobSearchResult> SearchResults { get; set; } = new List<JobSearchResult>();
    }

}
