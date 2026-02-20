namespace FrancProject.Models
{
    public class JobSearchResult
    {
        public long SearchId { get; set; }
        public JobSearchCache Search { get; set; } = null!;

        public long JobPostId { get; set; }
        public JobPost JobPost { get; set; } = null!;

        public int Position { get; set; }
    }

}
