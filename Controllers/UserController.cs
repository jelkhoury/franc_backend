using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interface;
using FrancProject.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Text;
using System.Security.Cryptography;
using static UserRepository;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{

    private readonly DataContext _context;
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;

    public UserController(IUserRepository userRepository, IConfiguration config, DataContext context)
    {
        _userRepo = userRepository;
        _config = config;
        _context = context;
    }

    // ---------------------------------------
    // SIGN UP
    // ---------------------------------------
    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] UserDto dto)
    {
        try
        {
            string token = await _userRepo.SignUp(dto);
            return Ok(new { message = "Signup successful!", token });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // VERIFY CODE
    // ---------------------------------------
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromQuery] string email, [FromQuery] string code)
    {
        try
        {
            bool ok = await _userRepo.VerifyVerificationCode(email, code);

            if (!ok)
                return BadRequest(new { message = "Invalid verification code." });

            return Ok(new { message = "Verification successful!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // SIGN IN
    // ---------------------------------------
    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        try
        {
            string token = await _userRepo.SignIn(request.Email, request.Password);
            return Ok(new { message = "Sign-in successful!", token });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ---------------------------------------
    // FORGOT PASSWORD
    // ---------------------------------------
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromQuery] string email)
    {
        try
        {
            string result = await _userRepo.ForgotPassword(email);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // RESET PASSWORD
    // ---------------------------------------
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            string result = await _userRepo.ResetPassword(dto.Email, dto.VerificationCode, dto.NewPassword);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // SEND MOCK INTERVIEW NOTIFICATION
    // (Authorized Users Only)
    // ---------------------------------------
    [Authorize]
    [HttpPost("send-mock-submission-notification")]
    public async Task<IActionResult> SendMockInterviewNotification([FromQuery] int userId)
    {
        try
        {
            await _userRepo.SendEmailAsync(userId);
            return Ok(new { message = "Notification emails sent to all admins." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // SEND PDF TO USER
    // (Authorized Users Only)
    // ---------------------------------------
    [Authorize]
    [HttpPost("send-pdf")]
    public async Task<IActionResult> SendPdfToUser([FromForm] int userId, [FromForm] IFormFile pdfFile)
    {
        try
        {
            if (pdfFile == null || pdfFile.Length == 0)
                return BadRequest(new { message = "PDF file is required." });

            using var ms = new MemoryStream();
            await pdfFile.CopyToAsync(ms);

            await _userRepo.SendPdfToUserAsync(userId, ms.ToArray(), pdfFile.FileName);

            return Ok(new { message = "PDF sent successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("get-all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            return Ok(await _userRepo.GetAllUsers());
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("add-user")]
    public async Task<IActionResult> AddUser([FromBody] UserCrudDto dto)
    {
        try
        {
            var result = await _userRepo.AddUser(dto);
            return Ok(new { message = "User added successfully.", user = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [Authorize]
    [HttpPut("update-user")]
    public async Task<IActionResult> UpdateUser([FromQuery] int id, [FromBody] UserCrudDto dto)
    {
        try
        {
            return Ok(new
            {
                message = "User updated successfully.",
                user = await _userRepo.UpdateUser(id, dto)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    [Authorize]
    [HttpDelete("delete-user")]
    public async Task<IActionResult> DeleteUser([FromQuery] int id)
    {
        try
        {
            await _userRepo.DeleteUser(id);
            return Ok(new { message = "User deleted successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

   

    [HttpGet("CanUserPerformAction")]
    public async Task<IActionResult> CanUserPerformAction([FromQuery] int userId, UserActionType action)
    {
        try
        {
            var canDo = await _userRepo.CanUserPerformActionAsync(userId,action);
            return Ok(new { userId, canDoMock = canDo });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode([FromQuery] string email)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return BadRequest(new { message = "User not found." });

          
            string code = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

           
            user.VerificationCode = code;
            user.IsVerified = false; 
            await _context.SaveChangesAsync();

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]));
            emailMessage.To.Add(MailboxAddress.Parse(email));
            emailMessage.Subject = "Verification Code";
            emailMessage.Body = new TextPart(TextFormat.Html)
            {
                Text = $"Your verification code is: <b>{code}</b>"
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _config["MAIL_HOST"],
                int.Parse(_config["MAIL_PORT"]),
                SecureSocketOptions.StartTls
            );
            await smtp.AuthenticateAsync(
                _config["MAIL_USERNAME"],
                _config["MAIL_PASSWORD"]
            );
            await smtp.SendAsync(emailMessage);
            await smtp.DisconnectAsync(true);

            return Ok(new
            {
                message = "Verification code sent successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("generate-token-by-email")]
    public async Task<IActionResult> GenerateTokenByEmail([FromQuery] string email)
    {
        try
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
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }






}



public class SignInRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class PdfEmailRequestDto
{
    public byte[] PdfBytes { get; set; }
    public string PdfFileName { get; set; }
}