using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class SDSResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [JsonIgnore]
        public User User { get; set; }
        [JsonIgnore]
        public SDSQuestion Question { get; set; }
        public int QuestionId { get; set; }
        public int AttemptNumber { get; set; }
        public string? SelectedValue { get; set; } // store "R", "A", etc. directly
        public string? CustomAnswer { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public int? SDSResultId { get; set; }
        [JsonIgnore]
        public SDSResult SDSResult { get; set; }

    }

}
