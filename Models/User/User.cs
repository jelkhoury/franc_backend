using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(6)]
        public string PasswordHash { get; set; }

        public string Role { get; set; }

        public string? VerificationCode { get; set; }
        public bool IsVerified { get; set; }

        public bool? CanDoMockInterview { get; set; } = true;

        public int? MockAttempts { get; set; }
        public int? CoverAttempts { get; set; }
        public int? ResumeAttempts { get; set; }
        public int? SDSAttempts { get; set; }
        public ICollection<FileRecord> Files { get; set; }
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public ICollection<JobComparison> JobComparisons { get; set; } = new List<JobComparison>();
    }
}
