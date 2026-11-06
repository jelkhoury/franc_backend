using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization; 

namespace FrancProject.Models
{
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string VideoUrl { get; set; } = null!;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;

        [JsonIgnore]
        public int? EvaluationReportId { get; set; }
        public EvaluationReport? EvaluationReport { get; set; }

        [JsonIgnore]
        public int? MockInterviewId { get; set; }
        public MockInterview? MockInterview { get; set; }
    }

}

