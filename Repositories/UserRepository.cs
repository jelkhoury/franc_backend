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
                Role = "User"
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
                throw new UnauthorizedAccessException("Invalid credentials or account not verified.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials or account not verified.");

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
    // SEND PDF EMAIL
    // --------------------------
    public async Task SendPdfToUserAsync(int userId, byte[] pdfBytes, string pdfName)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new ArgumentException("User not found");

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]));
            email.To.Add(MailboxAddress.Parse(user.Email));
            email.Subject = "Your PDF Document";

            var builder = new BodyBuilder
            {
                HtmlBody = $"<p>Hello {user.FirstName},</p><p>Please find your PDF attached.</p>"
            };

            builder.Attachments.Add(pdfName, pdfBytes);

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
    private async Task<string> CreateToken(User user)
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
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

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
                MockAttempts = u.MockAttempts
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
                MockAttempts = dto.MockAttempts ?? 0
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
                MockAttempts = user.MockAttempts
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

            await _context.SaveChangesAsync();

            return new UserResponseDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Role = user.Role,
                IsVerified = user.IsVerified,
                CanDoMockInterview = user.CanDoMockInterview,
                MockAttempts = user.MockAttempts
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


}
