using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace FrancProject.Models
{
    public class SDSAnswerOption
    {
        
        public int Id { get; set; }
        public int QuestionId { get; set; }
        [JsonIgnore]
        public SDSQuestion Question { get; set; }

        public string Text { get; set; }
        public string? Value { get; set; }
    }
}
