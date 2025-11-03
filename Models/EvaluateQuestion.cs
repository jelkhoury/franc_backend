using System;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class EvaluateQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int AnswerId { get; set; }
        public Answer Answer { get; set; }

        [Required]
        public int EvaluatorId { get; set; }
        public User Evaluator { get; set; }
    }
}
