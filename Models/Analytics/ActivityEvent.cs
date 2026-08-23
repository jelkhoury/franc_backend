using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models.Analytics
{
    public class ActivityEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(32)]
        public string ServiceKey { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string ActivityType { get; set; } = null!;

        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = null!;

        [MaxLength(256)]
        public string? ResultSummary { get; set; }

        [Required]
        public DateTime OccurredAt { get; set; }

        [Required]
        [MaxLength(64)]
        public string EntityId { get; set; } = null!;
    }
}
