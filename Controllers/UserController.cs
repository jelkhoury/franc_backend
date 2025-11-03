using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FrancProject.Dto;
using FrancProject.Interface;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UserController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] UserDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            string token = await _userRepository.SignUp(dto);
            return Ok(new { message = "Signup successful!", token });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromQuery] string email, [FromQuery] string code)
    {
        var isVerified = await _userRepository.VerifyVerificationCode(email, code);

        if (!isVerified)
            return BadRequest(new { message = "Invalid verification code." });

        return Ok(new { message = "Verification successful!" });
    }

    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        try
        {
            string token = await _userRepository.SignIn(request.Email, request.Password);
            return Ok(new { message = "Sign-in successful!", token });
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(new { message = "Invalid credentials or account not verified." });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromQuery] string email)
    {
        try
        {
            string result = await _userRepository.ForgotPassword(email);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            string result = await _userRepository.ResetPassword(dto.Email, dto.VerificationCode, dto.NewPassword);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("send-mock-submission-notification/{userId}")]
    public async Task<IActionResult> SendMockInterviewNotification(int userId)
    {
        try
        {
            await _userRepository.SendEmailAsync(userId);
            return Ok(new { message = "Notification emails sent to all admins." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while sending emails.", error = ex.Message });
        }
    }
    [HttpPost("send-pdf")]
    public async Task<IActionResult> SendPdfToUser([FromForm] int userId, [FromForm] IFormFile pdfFile)
    {
        if (pdfFile == null || pdfFile.Length == 0)
            return BadRequest(new { message = "PDF file is required." });

        byte[] pdfBytes;
        using (var ms = new MemoryStream())
        {
            await pdfFile.CopyToAsync(ms);
            pdfBytes = ms.ToArray();
        }

        try
        {
            await _userRepository.SendPdfToUserAsync(userId, pdfBytes, pdfFile.FileName);
            return Ok(new { message = "PDF sent successfully." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to send PDF email.", error = ex.Message });
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