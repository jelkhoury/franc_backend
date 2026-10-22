using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class AnswerEvaluationDto
    {
        public int AnswerId { get; set; }
        public string VideoUrl { get; set; } = null!;
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; } = null!;
        public string? Comment { get; set; }
        public string? Tips { get; set; }
        public int? Rating { get; set; }
    }
}
