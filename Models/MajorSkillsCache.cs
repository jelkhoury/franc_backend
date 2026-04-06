namespace FrancProject.Models
{
    public class MajorSkillsCache
    {
        public long Id { get; set; }

        public string SearchKey { get; set; } = null!;
        public string Faculty { get; set; } = null!;
        public string Major { get; set; } = null!;
        public string Level { get; set; } = null!;
        public string? Country { get; set; }

        public string ResultJson { get; set; } = null!;
        public string PromptVersion { get; set; } = null!;
        public string ModelName { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
