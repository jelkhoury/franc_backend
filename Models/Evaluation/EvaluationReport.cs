using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace FrancProject.Models
{
    public class EvaluationReport
    {
        [Key]
        public int Id { get; set; }

        public float? OverallRating { get; set; }

        public string? SummaryComment { get; set; }

        [Required]
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int UserId { get; set; }
        public User User { get; set; }

        public ICollection<Answer> Answers { get; set; }

        public ICollection<EvaluationReportSkill> SkillScores { get; set; }
    }
}
