using FrancProject.Dto;
using FrancProject.Models;
using System.Threading.Tasks;
using static UserRepository;

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
        Task SendPdfToUserAsync(int userId, byte[] documentBytes, string documentFileName);
        Task<List<UserResponseDto>> GetAllUsers();

        Task<UserResponseDto> AddUser(UserCrudDto dto);
        Task<UserResponseDto> UpdateUser(int id, UserCrudDto dto);
        Task<bool> DeleteUser(int id);
      
        Task<bool> CanUserPerformActionAsync(int userId, UserActionType action);
        Task<string> CreateToken(User user);
        Task<UserInfoDto> GetUserInfoAsync(int userId);
        Task<List<ChatSessionDto>> GetAllChatsAsync();

    }
}
