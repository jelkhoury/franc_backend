using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class AnswerWithQuestionDto
    {
        public int AnswerId { get; set; }
        public string VideoUrl { get; set; }
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; }
    }
}
