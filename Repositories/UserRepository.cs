using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Models;
using BCrypt.Net;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit.Text;
using FrancProject.Interface;

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    private readonly IConfiguration _configuration; 
    private readonly Random _random = new Random();

    public UserRepository(DataContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<string> SignUp(UserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.PasswordHash);

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PasswordHash = hashedPassword,
            VerificationCode = GenerateVerificationCode(),
            IsVerified = false,
            Role = "User",
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        SendVerificationCode(user.Email, user.VerificationCode);

        
        var token = await CreateToken(user);
        return token;
    }

    public async Task<string> SignIn(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) || !user.IsVerified)
        {
            throw new UnauthorizedAccessException("Invalid credentials or user not verified.");
        }

        var token = await CreateToken(user);
        return token;
    }

    private string GenerateVerificationCode()
    {
        return _random.Next(1000, 9999).ToString();
    }

    private void SendVerificationCode(string userEmail, string verificationCode)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_configuration["MAIL_FROM_ADDRESS"]));
        email.To.Add(MailboxAddress.Parse(userEmail));
        email.Subject = "Verification Code";
        email.Body = new TextPart(TextFormat.Html) { Text = $"Your verification code is: <b>{verificationCode}</b>" };

        using var smtp = new SmtpClient();

        smtp.Connect(_configuration["MAIL_HOST"], int.Parse(_configuration["MAIL_PORT"]), SecureSocketOptions.StartTls);
        smtp.Authenticate(_configuration["MAIL_USERNAME"], _configuration["MAIL_PASSWORD"]);
        smtp.Send(email);
        smtp.Disconnect(true);
    }


    public async Task SendEmailAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new ArgumentException("User not found");

        var fullName = $"{user.FirstName} {user.LastName}";
        var adminUsers = await _context.Users
            .Where(u => u.Role == "Admin")
            .ToListAsync();

        foreach (var admin in adminUsers)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_configuration["MAIL_FROM_ADDRESS"]));
            email.To.Add(MailboxAddress.Parse(admin.Email));
            email.Subject = "Mock Interview Submitted";
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = $"Mock interview has been submitted by: <b>{fullName}</b>"
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["MAIL_HOST"], int.Parse(_configuration["MAIL_PORT"]), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["MAIL_USERNAME"], _configuration["MAIL_PASSWORD"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }



    public async Task SendPdfToUserAsync(int userId, byte[] pdfBytes, string pdfFileName)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new ArgumentException("User not found");

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_configuration["MAIL_FROM_ADDRESS"]));
        email.To.Add(MailboxAddress.Parse(user.Email));
        email.Subject = "Your PDF Document";

        var builder = new BodyBuilder();

        builder.HtmlBody = $"<p>Dear {user.FirstName},</p><p>Please find your PDF document attached.</p>";

        builder.Attachments.Add(pdfFileName, pdfBytes, new ContentType("application", "pdf"));

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_configuration["MAIL_HOST"], int.Parse(_configuration["MAIL_PORT"]), SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_configuration["MAIL_USERNAME"], _configuration["MAIL_PASSWORD"]);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }




    public async Task<bool> VerifyVerificationCode(string userEmail, string verificationCode)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

        if (user == null || user.VerificationCode != verificationCode)
        {
            return false;
        }
        user.IsVerified = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> ForgotPassword(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new InvalidOperationException("No user associated with this email.");
        }

        user.VerificationCode = GenerateVerificationCode();
        await _context.SaveChangesAsync();

        SendVerificationCode(user.Email, user.VerificationCode);

        return "A verification code has been sent to your email.";
    }

    public async Task<string> ResetPassword(string email, string verificationCode, string newPassword)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || user.VerificationCode != verificationCode)
        {
            throw new InvalidOperationException("Invalid email or verification code.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.VerificationCode = null;
        await _context.SaveChangesAsync();

        return "Password reset successful.";
    }

    private async Task<string> CreateToken(User user)
    {
        List<Claim> claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
        new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
        new Claim(ClaimTypes.Role, user.Role)
    };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration.GetSection("AppSettings:Token").Value));

        var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: cred
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return await Task.FromResult(jwt);
    }


}
