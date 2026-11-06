using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class MockInterview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        [Required]
        public string Duration { get; set; } = null!;

        [JsonIgnore]
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();

        public int? NbOfTry { get; set; }

        public bool IsEvaluated { get; set; }
    }
}
