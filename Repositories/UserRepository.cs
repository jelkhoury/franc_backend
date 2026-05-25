using BCrypt.Net;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interface;
using FrancProject.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;

    public UserRepository(DataContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // --------------------------
    // SIGN UP
    // --------------------------
    public async Task<string> SignUp(UserDto dto)
    {
        try
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                throw new InvalidOperationException("Email is already registered.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.PasswordHash);

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = hashedPassword,
                VerificationCode = GenerateSecureVerificationCode(),
                IsVerified = false,
                Role = "User",
                CanDoMockInterview = true,
                MockAttempts = 2,
                CoverAttempts = 2,
                ResumeAttempts = 2,
                SDSAttempts=2

            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            SendVerificationCode(user.Email, user.VerificationCode);

            return await CreateToken(user);
        }
        catch (Exception ex)
        {
            throw new Exception($"SignUp failed: {ex.Message}");
        }
    }

    // --------------------------
    // SIGN IN
    // --------------------------
    public async Task<string> SignIn(string email, string password)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            if (!user.IsVerified)
                throw new UnauthorizedAccessException("Account not verified.");

            return await CreateToken(user);
        }
        catch (Exception ex)
        {
            throw new Exception($"SignIn failed: {ex.Message}");
        }
    }

    // --------------------------
    // VERIFY ACCOUNT
    // --------------------------
    public async Task<bool> VerifyVerificationCode(string email, string code)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new Exception("User not found.");

            if (user.VerificationCode != code)
                return false;

            user.IsVerified = true;
            user.VerificationCode = null;

            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"VerifyVerificationCode failed: {ex.Message}");
        }
    }

    // --------------------------
    // FORGOT PASSWORD
    // --------------------------
    public async Task<string> ForgotPassword(string email)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new InvalidOperationException("No user associated with this email.");

            user.VerificationCode = GenerateSecureVerificationCode();
            await _context.SaveChangesAsync();

            SendVerificationCode(user.Email, user.VerificationCode);

            return "A verification code has been sent to your email.";
        }
        catch (Exception ex)
        {
            throw new Exception($"ForgotPassword failed: {ex.Message}");
        }
    }

    // --------------------------
    // RESET PASSWORD
    // --------------------------
    public async Task<string> ResetPassword(string email, string code, string newPassword)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || user.VerificationCode != code)
                throw new InvalidOperationException("Invalid email or verification code.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.VerificationCode = null;

            await _context.SaveChangesAsync();

            return "Password reset successful.";
        }
        catch (Exception ex)
        {
            throw new Exception($"ResetPassword failed: {ex.Message}");
        }
    }

    // --------------------------
    // SEND MOCK INTERVIEW EMAIL
    // --------------------------
    public async Task SendEmailAsync(int userId)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new ArgumentException("User not found");

            var admins = await _context.Users
                .Where(u => u.Role == "Admin")
                .ToListAsync();

            foreach (var admin in admins)
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]));
                email.To.Add(MailboxAddress.Parse(admin.Email));
                email.Subject = "Mock Interview Submitted";
                email.Body = new TextPart(TextFormat.Html)
                {
                    Text = $"Mock interview was submitted by <b>{user.FirstName} {user.LastName}</b>."
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["MAIL_HOST"], int.Parse(_config["MAIL_PORT"]), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["MAIL_USERNAME"], _config["MAIL_PASSWORD"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"SendEmailAsync failed: {ex.Message}");
        }
    }

    // --------------------------
    // SEND MOCK INTERVIEW REPORT (WORD)
    // --------------------------
    public async Task SendPdfToUserAsync(int userId, byte[] documentBytes, string documentFileName)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new ArgumentException("User not found");

            if (documentBytes == null || documentBytes.Length == 0)
                throw new ArgumentException("Report document is empty.");

            var attachmentName = EnsureDocxFileName(documentFileName);

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]));
            email.To.Add(MailboxAddress.Parse(user.Email));
            email.Subject = "Mock Interview Evaluation Report – CCD Department";

            var builder = new BodyBuilder
            {
                HtmlBody = BuildMockInterviewReportEmailBody(user.FirstName)
            };

            builder.Attachments.Add(attachmentName, documentBytes, ContentType.Parse(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_config["MAIL_HOST"], int.Parse(_config["MAIL_PORT"]), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_config["MAIL_USERNAME"], _config["MAIL_PASSWORD"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"SendPdfToUserAsync failed: {ex.Message}");
        }
    }

    private static string EnsureDocxFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "MockInterviewReport.docx";

        return Path.GetExtension(fileName).Equals(".docx", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : Path.ChangeExtension(fileName, ".docx");
    }

    private static string BuildMockInterviewReportEmailBody(string firstName)
    {
        var studentName = string.IsNullOrWhiteSpace(firstName)
            ? "Student"
            : firstName.Trim();

        return $"""
            <div style="font-family: Arial, Helvetica, sans-serif; color: #222; line-height: 1.6; max-width: 640px;">
                <p>Dear {studentName},</p>
                <p>
                    We hope this message finds you well. The Career and Community Development (CCD) Department has completed
                    its review of your mock interview session conducted through <strong>Franc</strong>.
                </p>
                <p>
                    Please find attached your official evaluation report, which summarizes your performance, feedback,
                    and recommendations to help you prepare for future interviews.
                </p>
                <p>
                    We encourage you to review the report carefully and use the feedback to continue developing your
                    interview skills. If you have any questions or would like to discuss your results further, please
                    contact the CCD Department.
                </p>
                <p>
                    Best regards,<br/>
                    <strong>Career and Community Development (CCD) Department</strong><br/>
                    University Career Services
                </p>
                <p style="font-size: 12px; color: #666; margin-top: 24px;">
                    This message was sent following the completion of your Franc mock interview evaluation.
                </p>
            </div>
            """;
    }

    // --------------------------
    // HELPER: SECURE CODE
    // --------------------------
    private string GenerateSecureVerificationCode()
    {
        return RandomNumberGenerator.GetInt32(1000, 9999).ToString();
    }

    // --------------------------
    // HELPER: JWT TOKEN
    // --------------------------
    public async Task<string> CreateToken(User user)
    {
        try
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["AppSettings:Token"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                expires: DateTime.UtcNow.AddDays(1),
                claims: claims,
                signingCredentials: creds
            );

            return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
        catch (Exception ex)
        {
            throw new Exception($"CreateToken failed: {ex.Message}");
        }
    }

    // --------------------------
    // HELPER: SEND VERIFICATION EMAIL
    // --------------------------
    private void SendVerificationCode(string emailTo, string code)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]));
        email.To.Add(MailboxAddress.Parse(emailTo));
        email.Subject = "Verification Code";
        email.Body = new TextPart(TextFormat.Html)
        {
            Text = $"Your verification code is: <b>{code}</b>"
        };

        using var smtp = new SmtpClient();
        smtp.Connect(_config["MAIL_HOST"], int.Parse(_config["MAIL_PORT"]), SecureSocketOptions.StartTls);
        smtp.Authenticate(_config["MAIL_USERNAME"], _config["MAIL_PASSWORD"]);
        smtp.Send(email);
        smtp.Disconnect(true);
    }

    public async Task<List<UserResponseDto>> GetAllUsers()
    {
        return await _context.Users
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                FullName = $"{u.FirstName} {u.LastName}",
                Email = u.Email,
                Role = u.Role,
                IsVerified = u.IsVerified,
                CanDoMockInterview = u.CanDoMockInterview,
                MockAttempts = u.MockAttempts,
               SDSAttempts=u.SDSAttempts,
               CoverAttempts=u.CoverAttempts,
               ResumeAttempts = u.ResumeAttempts
            })
            .ToListAsync();
    }




    public async Task<UserResponseDto> AddUser(UserCrudDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                throw new Exception("Email and password are required.");

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                throw new InvalidOperationException("Email already registered.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                FirstName = dto.FirstName ?? "",
                LastName = dto.LastName ?? "",
                Email = dto.Email,
                PasswordHash = hashedPassword,

                Role = dto.Role ?? "User",
                IsVerified = true,
                VerificationCode = null,

                CanDoMockInterview = dto.CanDoMockInterview ?? true,
                MockAttempts = dto.MockAttempts ?? 0,
                CoverAttempts=dto.CoverAttempts ?? 0,
                ResumeAttempts = dto.ResumeAttempts ?? 0,
                SDSAttempts= dto.SDSAttempts ?? 0


            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserResponseDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Role = user.Role,
                IsVerified = user.IsVerified,
                CanDoMockInterview = user.CanDoMockInterview,
                MockAttempts = user.MockAttempts,
                CoverAttempts = user.CoverAttempts,
                ResumeAttempts = user.ResumeAttempts,
                SDSAttempts = user.SDSAttempts,

            };
        }
        catch (Exception ex)
        {
            throw new Exception($"AddUser failed: {ex.Message}");
        }
    }



    public async Task<UserResponseDto> UpdateUser(int id, UserCrudDto dto)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                throw new Exception("User not found.");

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.Email = dto.Email ?? user.Email;
            user.Role = dto.Role ?? user.Role;

       
            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            user.CanDoMockInterview = dto.CanDoMockInterview ?? user.CanDoMockInterview;
            user.MockAttempts = dto.MockAttempts ?? user.MockAttempts;
            user.ResumeAttempts = dto.ResumeAttempts ?? user.ResumeAttempts;
            user.CoverAttempts = dto.CoverAttempts ?? user.CoverAttempts;
            user.SDSAttempts = dto.SDSAttempts ?? user.SDSAttempts;



            await _context.SaveChangesAsync();

            return new UserResponseDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Role = user.Role,
                IsVerified = user.IsVerified,
                CanDoMockInterview = user.CanDoMockInterview,
                MockAttempts = user.MockAttempts,
                CoverAttempts=user.CoverAttempts,
                ResumeAttempts = user.ResumeAttempts,
                SDSAttempts = user.SDSAttempts,

            };
        }
        catch (Exception ex)
        {
            throw new Exception($"UpdateUser failed: {ex.Message}");
        }
    }


    public async Task<bool> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                throw new Exception("User not found.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"DeleteUser failed: {ex.Message}");
        }
    }

    public async Task<bool> CanDoMockInterviewAsync(User user)
    {
        if (user.CanDoMockInterview == false)
            return false;

        if ((user.MockAttempts ?? 0) <= 0)
            return false;

        int count = await _context.EvaluationReports
            .CountAsync(r => r.UserId == user.Id);

        if (count >= 1)
            return false;

        return true;
    }

    public async Task<bool> CanUserPerformActionAsync(int userId, UserActionType action)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        switch (action)
        {
            case UserActionType.MockInterview:
                return await CanDoMockInterviewAsync(user);

            case UserActionType.SDS:
                return (user.SDSAttempts ?? 0) > 0;

            case UserActionType.Resume:
                return (user.ResumeAttempts ?? 0) > 0;

            case UserActionType.CoverLetter:
                return (user.CoverAttempts ?? 0) > 0;

            default:
                throw new ArgumentOutOfRangeException(nameof(action), "Invalid action type");
        }
    }

    public async Task<UserInfoDto> GetUserInfoAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserInfoDto
            {
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Role = u.Role,
                IsVerified = u.IsVerified,
                MockAttempts = u.MockAttempts,
                CoverAttempts = u.CoverAttempts,
                ResumeAttempts = u.ResumeAttempts,
                SDSAttempts = u.SDSAttempts
            })
            .FirstOrDefaultAsync();

        if (user == null)
            throw new Exception("User not found.");

        return user;
    }

    public async Task<List<ChatSessionDto>> GetAllChatsAsync()
    {
        var raw = await _context.ChatMessages
            .Include(x => x.User)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var result = raw
            .GroupBy(x => x.SessionId)
            .Select(g => new ChatSessionDto
            {
                SessionId = g.Key,
                Email = g.First().User.Email,  
                Messages = g.Select(m => new ChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt
                }).ToList()
            })
            .ToList();

        return result;
    }



    public enum UserActionType
    {
        MockInterview,
        SDS,
        Resume,
        CoverLetter
    }



}
