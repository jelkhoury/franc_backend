namespace FrancProject.Models
{
    public class JobSearchCache
    {
        public long Id { get; set; }

        public string SearchKey { get; set; } = null!;
        public string QueryText { get; set; } = null!;
        public string LocationUsed { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ExpiresAt { get; set; }   // 48h TTL

        public ICollection<JobSearchResult> Results { get; set; } = new List<JobSearchResult>();
    }

}
