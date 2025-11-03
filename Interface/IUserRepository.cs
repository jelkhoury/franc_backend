using System.Threading.Tasks;
using FrancProject.Dto;

namespace FrancProject.Interface
{
    public interface IUserRepository
    {
        Task<string> SignUp(UserDto dto);
        Task<bool> VerifyVerificationCode(string userEmail, string verificationCode);
        Task<string> SignIn(string email, string password);
        Task SendEmailAsync(int userId);
        Task<string> ForgotPassword(string email);
        Task<string> ResetPassword(string email, string verificationCode, string newPassword);
        Task SendPdfToUserAsync(int userId, byte[] pdfBytes, string pdfFileName);
    }
}
