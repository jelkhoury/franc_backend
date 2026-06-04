using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class AnswerEvaluationDto
    {
        public int AnswerId { get; set; }
        public string VideoUrl { get; set; }
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string? Comment { get; set; }
        public string? Tips { get; set; }
        public int? Rating { get; set; }
    }
}
