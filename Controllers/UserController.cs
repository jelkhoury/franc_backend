using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FrancProject.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IUserService _userRepo;
    private readonly IEmailService _emailService;
    private readonly IFileUploadSecurityService _uploadSecurity;

    public UserController(
        IUserService userService,
        DataContext context,
        IEmailService emailService,
        IFileUploadSecurityService uploadSecurity)
    {
        _userRepo = userService;
        _context = context;
        _emailService = emailService;
        _uploadSecurity = uploadSecurity;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] UserDto dto)
    {
        string token = await _userRepo.SignUp(dto);
        return Ok(new { message = "Signup successful!", token });
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromQuery] string email, [FromQuery] string code)
    {
        bool ok = await _userRepo.VerifyVerificationCode(email, code);

        if (!ok)
            return BadRequest(new { message = "Invalid verification code." });

        return Ok(new { message = "Verification successful!" });
    }

    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        string token = await _userRepo.SignIn(request.Email, request.Password);
        return Ok(new { message = "Sign-in successful!", token });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromQuery] string email)
    {
        string result = await _userRepo.ForgotPassword(email);
        return Ok(new { message = result });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        string result = await _userRepo.ResetPassword(dto.Email, dto.VerificationCode, dto.NewPassword);
        return Ok(new { message = result });
    }

    [Authorize]
    [HttpPost("send-mock-submission-notification")]
    public async Task<IActionResult> SendMockInterviewNotification([FromQuery] int userId)
    {
        await _userRepo.SendEmailAsync(userId);
        return Ok(new { message = "Notification emails sent to all admins." });
    }

    [Authorize]
    [HttpPost("send-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendPdfToUser([FromForm] SendWordReportFormDto model)
    {
        if (model.ReportFile == null || model.ReportFile.Length == 0)
            return BadRequest(new { message = "Word report file is required." });

        await _uploadSecurity.ValidateAsync(model.ReportFile, FileUploadCategory.WordReport);

        using var ms = new MemoryStream();
        await model.ReportFile.CopyToAsync(ms);

        await _userRepo.SendPdfToUserAsync(model.UserId, ms.ToArray(), model.ReportFile.FileName);

        return Ok(new { message = "Mock interview report sent successfully." });
    }

    [Authorize]
    [HttpGet("get-all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        return Ok(await _userRepo.GetAllUsers());
    }

    [Authorize]
    [HttpPost("add-user")]
    public async Task<IActionResult> AddUser([FromBody] UserCrudDto dto)
    {
        var result = await _userRepo.AddUser(dto);
        return Ok(new { message = "User added successfully.", user = result });
    }

    [Authorize]
    [HttpPut("update-user")]
    public async Task<IActionResult> UpdateUser([FromQuery] int id, [FromBody] UserCrudDto dto)
    {
        return Ok(new
        {
            message = "User updated successfully.",
            user = await _userRepo.UpdateUser(id, dto)
        });
    }

    [Authorize]
    [HttpDelete("delete-user")]
    public async Task<IActionResult> DeleteUser([FromQuery] int id)
    {
        await _userRepo.DeleteUser(id);
        return Ok(new { message = "User deleted successfully." });
    }

    [HttpGet("CanUserPerformAction")]
    public async Task<IActionResult> CanUserPerformAction([FromQuery] int userId, UserActionType action)
    {
        var canDo = await _userRepo.CanUserPerformActionAsync(userId, action);
        return Ok(new { userId, canDoMock = canDo });
    }

    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode([FromQuery] string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return BadRequest(new { message = "User not found." });

        string code = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

        user.VerificationCode = code;
        user.IsVerified = false;
        await _context.SaveChangesAsync();

        await _emailService.SendVerificationCodeAsync(email, code);

        return Ok(new { message = "Verification code sent successfully." });
    }

    [HttpPost("generate-token-by-email")]
    public async Task<IActionResult> GenerateTokenByEmail([FromQuery] string email)
    {
        var user = await _context.Users
            .Where(u => u.Email == email)
            .Select(u => new User
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return BadRequest(new { message = "User not found." });

        var token = await _userRepo.CreateToken(user);

        return Ok(new { token });
    }

    [Authorize]
    [HttpGet("GetUserInfo")]
    public async Task<IActionResult> GetMyInfo()
    {
        int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userInfo = await _userRepo.GetUserInfoAsync(userId);
        return Ok(userInfo);
    }

    [HttpGet("chats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllChats()
    {
        var result = await _userRepo.GetAllChatsAsync();
        return Ok(result);
    }
}
