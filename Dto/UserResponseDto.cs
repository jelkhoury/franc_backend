namespace FrancProject.Dto
{
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }

        public string Email { get; set; }

        public string Role { get; set; }

        public bool IsVerified { get; set; }

        public bool? CanDoMockInterview { get; set; }

        public int? MockAttempts { get; set; }
    }

}
