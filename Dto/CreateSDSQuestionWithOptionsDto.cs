using System.Collections.Generic;
using FrancProject.Models;

namespace FrancProject.Dto
{

    public class CreateSDSQuestionWithOptionsDto
    {
        public string SectionName { get; set; }
        public string Text { get; set; }
        public QuestionType Type { get; set; }
        public List<CreateSDSAnswerOptionDto> AnswerOptions { get; set; } = new();
        
    }



    
}

