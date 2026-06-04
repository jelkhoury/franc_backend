using System;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class FileRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; }

        // Resume | CoverLetter | JobAdd
        [Required]
        [MaxLength(50)]
        public string Title { get; set; }

        public string? ResumeUrl { get; set; }
        public string? CoverUrl { get; set; }
        public string? JobAddUrl { get; set; }

        public string? AiEvaluation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
