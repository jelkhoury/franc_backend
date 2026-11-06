namespace FrancProject.Dto
{
    public class UserInfoDto
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;

        public bool IsVerified { get; set; }

        public int? MockAttempts { get; set; }
        public int? CoverAttempts { get; set; }
        public int? ResumeAttempts { get; set; }
        public int? SDSAttempts { get; set; }
    }
}
