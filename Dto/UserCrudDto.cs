namespace FrancProject.Dto
{
    public class UserCrudDto
    {
        public int? Id { get; set; }  // only used for update/delete if needed

        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string? Email { get; set; }

        // Plain password (will be hashed automatically)
        public string? Password { get; set; }

        public string? Role { get; set; }

        public bool? CanDoMockInterview { get; set; }
        public int? MockAttempts { get; set; }
        public int? CoverAttempts { get; set; }
        public int? ResumeAttempts { get; set; }
        public int? SDSAttempts { get; set; }
    }

}
