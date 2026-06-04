using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrancProject.Models
{
    public class JobComparison
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        [MaxLength(200)]
        public string JobAName { get; set; }

        [Required]
        [MaxLength(200)]
        public string JobBName { get; set; }

        [Required]
        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
        public ICollection<JobComparisonAnswer> Answers { get; set; } = new List<JobComparisonAnswer>();
        public string? ExcelResultUrl { get; set; }
    }
}
