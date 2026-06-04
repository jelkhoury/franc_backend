using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    /// <summary>
    /// Represents a predefined criterion/question for job comparison.
    /// This table stores the criteria that users will rate during the comparison.
    /// </summary>
    public class JobComparisonCriterion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Name { get; set; }

        [Required]
        [MaxLength(100)]
        public string Section { get; set; }

        [Required]
        [MaxLength(20)]
        public string Category { get; set; } // "HEAD" or "HEART"

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
