using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class SDSResult
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; }

        [MaxLength(10)]
        public string? HollandCode { get; set; }

        public string? AIFeedback { get; set; }
        public int AttemptNumber { get; set; }
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public ICollection<SDSResponse> Responses { get; set; } = new List<SDSResponse>();
    }
}
