using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Extensions;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // --------------------------------------------------
    // ANONYMOUS: ACCOUNT LIFECYCLE
    // --------------------------------------------------

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] UserDto dto)
    {
        string token = await _userRepo.SignUp(dto);
        return Ok(new { message = "Signup successful!", token });
    }

    /// <summary>
    /// Confirms an account and returns the JWT for the now-verified user.
    /// </summary>
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromQuery] string email, [FromQuery] string code)
    {
        string? token = await _userRepo.VerifyVerificationCode(email, code);

        if (token is null)
            return BadRequest(new { message = "Invalid verification code." });

        return Ok(new { message = "Verification successful!", token });
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

    /// <summary>
    /// Issues a fresh verification code. The response is deliberately identical whether or
    /// not the email exists, so this endpoint cannot be used to enumerate accounts.
    /// </summary>
    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode([FromQuery] string email)
    {
        const string genericResponse = "If that email is registered, a verification code has been sent.";

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return Ok(new { message = genericResponse });

        string code = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

        // Note: IsVerified is intentionally left untouched. Clearing it here would let
        // anyone lock an existing account out of sign-in just by knowing its email.
        user.VerificationCode = code;
        await _context.SaveChangesAsync();

        await _emailService.SendVerificationCodeAsync(email, code);

        return Ok(new { message = genericResponse });
    }

    // --------------------------------------------------
    // AUTHENTICATED: SELF-SERVICE
    // --------------------------------------------------

    [Authorize]
    [HttpGet("GetUserInfo")]
    public async Task<IActionResult> GetMyInfo()
    {
        var userInfo = await _userRepo.GetUserInfoAsync(User.GetUserId());
        return Ok(userInfo);
    }

    /// <summary>Reports the caller's remaining attempts. Admins may inspect another user via <paramref name="userId"/>.</summary>
    [Authorize]
    [HttpGet("CanUserPerformAction")]
    public async Task<IActionResult> CanUserPerformAction(
        [FromQuery] UserActionType action,
        [FromQuery] int? userId = null)
    {
        int targetUserId = User.ResolveAccessibleUserId(userId);
        var canDo = await _userRepo.CanUserPerformActionAsync(targetUserId, action);
        return Ok(new { userId = targetUserId, canDoMock = canDo });
    }

    [Authorize]
    [HttpPost("send-mock-submission-notification")]
    public async Task<IActionResult> SendMockInterviewNotification([FromQuery] int? userId = null)
    {
        await _userRepo.SendEmailAsync(User.ResolveAccessibleUserId(userId));
        return Ok(new { message = "Notification emails sent to all admins." });
    }

    [Authorize]
    [HttpPost("send-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendPdfToUser([FromForm] SendWordReportFormDto model)
    {
        if (model.ReportFile == null || model.ReportFile.Length == 0)
            return BadRequest(new { message = "Word report file is required." });

        int targetUserId = User.ResolveAccessibleUserId(model.UserId);

        await _uploadSecurity.ValidateAsync(model.ReportFile, FileUploadCategory.WordReport);

        using var ms = new MemoryStream();
        await model.ReportFile.CopyToAsync(ms);

        await _userRepo.SendPdfToUserAsync(targetUserId, ms.ToArray(), model.ReportFile.FileName);

        return Ok(new { message = "Mock interview report sent successfully." });
    }

    // --------------------------------------------------
    // ADMIN: USER MANAGEMENT
    // --------------------------------------------------

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpGet("get-all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        return Ok(await _userRepo.GetAllUsers());
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpPost("add-user")]
    public async Task<IActionResult> AddUser([FromBody] UserCrudDto dto)
    {
        var result = await _userRepo.AddUser(dto);
        return Ok(new { message = "User added successfully.", user = result });
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpPut("update-user")]
    public async Task<IActionResult> UpdateUser([FromQuery] int id, [FromBody] UserCrudDto dto)
    {
        return Ok(new
        {
            message = "User updated successfully.",
            user = await _userRepo.UpdateUser(id, dto)
        });
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpDelete("delete-user")]
    public async Task<IActionResult> DeleteUser([FromQuery] int id)
    {
        await _userRepo.DeleteUser(id);
        return Ok(new { message = "User deleted successfully." });
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpGet("chats")]
    public async Task<IActionResult> GetAllChats()
    {
        var result = await _userRepo.GetAllChatsAsync();
        return Ok(result);
    }
}
