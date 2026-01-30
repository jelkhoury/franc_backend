using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class JobComparisonAnswer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int JobComparisonId { get; set; }

        [ForeignKey("JobComparisonId")]
        [JsonIgnore]
        public JobComparison JobComparison { get; set; }

        [Required]
        public int CriterionId { get; set; }

        [Required]
        [Range(0, 5)]
        public int Weight { get; set; }

        [Required]
        [Range(0, 5)]
        public int ScoreA { get; set; }

        [Required]
        [Range(0, 5)]
        public int ScoreB { get; set; }

        [Required]
        public bool NotApplicable { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
