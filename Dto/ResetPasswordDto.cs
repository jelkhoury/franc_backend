namespace FrancProject.Dto
{
    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string VerificationCode { get; set; }
        public string NewPassword { get; set; }
    }
}
