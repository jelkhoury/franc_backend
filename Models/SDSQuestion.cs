using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class SDSQuestion
    {
       
        public int Id { get; set; }

        public int SectionId { get; set; }
        [JsonIgnore]
        public SDSSection Section { get; set; }

        public string Text { get; set; }   
        public QuestionType Type { get; set; } 

        public ICollection<SDSAnswerOption> AnswerOptions { get; set; }
    }

    public enum QuestionType
    {
        Radio = 1,
        Checkbox = 2,
        Select = 3,
        Slider = 4,
        TextBox = 5,
        TextArea = 6
    }
}


