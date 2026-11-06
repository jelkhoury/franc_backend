using BCrypt.Net;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Exceptions;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FrancProject.Services;

public class UserService : IUserService
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _clock;
    private const string VerifyEmail = "verify_email";
    private const string ResetPasswordPurpose = "password_reset";
    private const int MaxCodeAttempts = 5;
    private const string VerificationSent = "If that email is registered, a verification code has been sent.";
    private const string ResetSent = "If that email is registered, a reset code has been sent.";

    public UserService(DataContext context, IConfiguration config, IEmailService emailService, TimeProvider? clock = null)
    {
        _context = context;
        _config = config;
        _emailService = emailService;
        _clock = clock ?? TimeProvider.System;
    }

    // --------------------------
    // SIGN UP
    // --------------------------
    public async Task SignUp(UserDto dto)
    {
        var email = dto.Email;
        if (await _context.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("Email is already registered.");

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.PasswordHash);

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = email,
            PasswordHash = hashedPassword,
            IsVerified = false,
            Role = "User",
            CanDoMockInterview = true,
            MockAttempts = 2,
            CoverAttempts = 2,
            ResumeAttempts = 2,
            SDSAttempts = 2,
            CreatedAt = DateTime.UtcNow
        };
        IssueChallenge(user, VerifyEmail);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _emailService.SendVerificationCodeAsync(user.Email, user.VerificationCode!);
    }

    // --------------------------
    // SIGN IN
    // --------------------------
    public async Task<string> SignIn(string email, string password)
    {
        var user = await FindByEmailAsync(email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        if (!user.IsVerified)
            throw new AccountNotVerifiedException();

        return await CreateToken(user);
    }

    // --------------------------
    // VERIFY ACCOUNT
    // --------------------------
    public async Task<string?> VerifyVerificationCode(string email, string code)
    {
        var user = await FindByEmailAsync(email);
        if (user == null || user.IsVerified ||
            !await CompleteChallengeAsync(user, code, VerifyEmail, u => u.IsVerified = true))
            return null;

        // The account is now proven to belong to the caller, so issuing a token here
        // removes any need for a separate "give me a token for this email" endpoint.
        return await CreateToken(user);
    }

    public async Task<string> SendVerificationCodeAsync(string email)
    {
        var user = await FindByEmailAsync(email);
        if (user != null && !user.IsVerified) await SendChallengeAsync(user, VerifyEmail);
        return VerificationSent;
    }

    private static bool CodesMatch(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));

    // --------------------------
    // FORGOT PASSWORD
    // --------------------------
    public async Task<string> ForgotPassword(string email)
    {
        var user = await FindByEmailAsync(email);
        if (user != null) await SendChallengeAsync(user, ResetPasswordPurpose);
        return ResetSent;
    }

    // --------------------------
    // RESET PASSWORD
    // --------------------------
    public async Task<string> ResetPassword(string email, string code, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new ArgumentException("Password must contain at least six characters.");
        var user = await FindByEmailAsync(email);
        if (user == null || !await CompleteChallengeAsync(user, code, ResetPasswordPurpose,
                u => u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword)))
            throw new InvalidOperationException("Invalid, expired, or exhausted verification code. Request a new code.");

        return "Password reset successful.";
    }

    // --------------------------
    // SEND MOCK INTERVIEW EMAIL
    // --------------------------
    public async Task SendEmailAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new KeyNotFoundException("User not found");

        var admins = await _context.Users
            .Where(u => u.Role == "Admin")
            .ToListAsync();

        var submitterName = $"{user.FirstName} {user.LastName}";
        foreach (var admin in admins)
        {
            await _emailService.SendMockInterviewSubmittedNotificationAsync(
                admin.Email,
                submitterName);
        }
    }

    // --------------------------
    // SEND MOCK INTERVIEW REPORT (WORD)
    // --------------------------
    public async Task SendPdfToUserAsync(int userId, byte[] documentBytes, string documentFileName)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new KeyNotFoundException("User not found");

        await _emailService.SendMockInterviewReportAsync(
            user.Email,
            user.FirstName,
            documentBytes,
            documentFileName);
    }


    // --------------------------
    // HELPER: SECURE CODE
    // --------------------------
    private Task<User?> FindByEmailAsync(string email)
    {
        // Match the existing database uniqueness and admin-write rules exactly.
        return _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    private void IssueChallenge(User user, string purpose)
    {
        string code;
        do { code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); }
        while (code == user.VerificationCode);
        user.VerificationCode = code;
        user.VerificationCodePurpose = purpose;
        user.VerificationCodeExpiresAt = _clock.GetUtcNow().AddMinutes(10);
        user.VerificationCodeSentAt = _clock.GetUtcNow();
        user.VerificationCodeAttempts = 0;
        user.VerificationCodeVersion++;
    }

    private async Task SendChallengeAsync(User user, string purpose)
    {
        // Persisted per-account cooldown also works across workers and server restarts.
        if (user.VerificationCodeSentAt > _clock.GetUtcNow().AddSeconds(-60)) return;
        IssueChallenge(user, purpose);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { _context.Entry(user).State = EntityState.Detached; return; }
        await _emailService.SendVerificationCodeAsync(user.Email, user.VerificationCode!);
    }

    private async Task<bool> CompleteChallengeAsync(User user, string code, string purpose, Action<User> onSuccess)
    {
        if (user.VerificationCode == null || user.VerificationCodePurpose != purpose
            || user.VerificationCodeExpiresAt == null || user.VerificationCodeExpiresAt <= _clock.GetUtcNow()
            || user.VerificationCodeAttempts >= MaxCodeAttempts) return false;
        var matches = CodesMatch(user.VerificationCode, code);
        user.VerificationCodeVersion++;
        if (matches) onSuccess(user);
        else user.VerificationCodeAttempts++;
        if (matches || user.VerificationCodeAttempts >= MaxCodeAttempts) {
            user.VerificationCode = null;
            user.VerificationCodePurpose = null;
            user.VerificationCodeExpiresAt = null;
        }
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { _context.Entry(user).State = EntityState.Detached; return false; }
        return matches;
    }

    // --------------------------
    // HELPER: JWT TOKEN
    // --------------------------
    public async Task<string> CreateToken(User user)
    {
        if (!user.IsVerified) throw new UnauthorizedAccessException("Account not verified.");
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var signingKey = _config["AppSettings:Token"]
            ?? throw new InvalidOperationException("AppSettings:Token is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddDays(1),
            claims: claims,
            signingCredentials: creds
        );

        return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
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
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Email and password are required.");

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
            CoverAttempts = dto.CoverAttempts ?? 0,
            ResumeAttempts = dto.ResumeAttempts ?? 0,
            SDSAttempts = dto.SDSAttempts ?? 0,
            CreatedAt = DateTime.UtcNow
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



    public async Task<UserResponseDto> UpdateUser(int id, UserCrudDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new KeyNotFoundException("User not found.");

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
            CoverAttempts = user.CoverAttempts,
            ResumeAttempts = user.ResumeAttempts,
            SDSAttempts = user.SDSAttempts,
        };
    }


    public async Task<bool> DeleteUser(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new KeyNotFoundException("User not found.");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return true;
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
            throw new KeyNotFoundException("User not found");

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
            throw new KeyNotFoundException("User not found.");

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
}
