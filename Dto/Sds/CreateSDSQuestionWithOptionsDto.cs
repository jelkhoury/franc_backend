using System.Collections.Generic;
using FrancProject.Models;

namespace FrancProject.Dto
{

    public class CreateSDSQuestionWithOptionsDto
    {
        public string SectionName { get; set; } = null!;
        public string Text { get; set; } = null!;
        public QuestionType Type { get; set; }
        public List<CreateSDSAnswerOptionDto> AnswerOptions { get; set; } = new();
        
    }



    
}

