using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class UserMockInterviewAnswersDto
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public int MockInterviewId { get; set; }
        public string MockInterviewTitle { get; set; } 
        public List<AnswerWithQuestionDto> Answers { get; set; }
        public int? NbOfTry { get; set; }
        public bool IsEvaluated { get; set; }
    }
}
