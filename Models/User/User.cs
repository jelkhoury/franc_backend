using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string PasswordHash { get; set; } = null!;

        public string Role { get; set; } = null!;

        public string? VerificationCode { get; set; }
        public DateTimeOffset? VerificationCodeExpiresAt { get; set; }
        public DateTimeOffset? VerificationCodeSentAt { get; set; }
        [MaxLength(30)]
        public string? VerificationCodePurpose { get; set; }
        public int VerificationCodeAttempts { get; set; }
        public int VerificationCodeVersion { get; set; }
        public bool IsVerified { get; set; }

        public bool? CanDoMockInterview { get; set; } = true;

        public int? MockAttempts { get; set; }
        public int? CoverAttempts { get; set; }
        public int? ResumeAttempts { get; set; }
        public int? SDSAttempts { get; set; }

        public DateTime? CreatedAt { get; set; }

        [MaxLength(200)]
        public string? Faculty { get; set; }

        [MaxLength(200)]
        public string? Major { get; set; }

        public ICollection<FileRecord> Files { get; set; } = new List<FileRecord>();
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public ICollection<JobComparison> JobComparisons { get; set; } = new List<JobComparison>();
    }
}
