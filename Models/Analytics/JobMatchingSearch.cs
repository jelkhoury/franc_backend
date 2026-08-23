using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models.Analytics
{
    public class JobMatchingSearch
    {
        public long Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(32)]
        public string SearchType { get; set; } = null!;

        [MaxLength(200)]
        public string? Faculty { get; set; }

        [MaxLength(200)]
        public string? Major { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(500)]
        public string? QueryText { get; set; }

        public int ResultsCount { get; set; }

        public DateTimeOffset SearchedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
