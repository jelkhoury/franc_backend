namespace FrancProject.Dto
{
    public class UserInfoDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }

        public bool IsVerified { get; set; }

        public int? MockAttempts { get; set; }
        public int? CoverAttempts { get; set; }
        public int? ResumeAttempts { get; set; }
        public int? SDSAttempts { get; set; }
    }
}
