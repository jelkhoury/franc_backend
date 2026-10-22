using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class AnswerWithQuestionDto
    {
        public int AnswerId { get; set; }
        public string VideoUrl { get; set; } = null!;
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; } = null!;
    }
}
