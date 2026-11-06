using System.IdentityModel.Tokens.Jwt;
using FrancProject.Controllers;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using FrancProject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FrancProject.Tests;

public class AccountVerificationTests
{
    internal const string SigningKey = "synthetic-test-key-at-least-thirty-two-bytes";
    private readonly ManualClock clock = new();
    private readonly RecordingEmail email = new();
    private static readonly UserDto Signup = new() {FirstName="Test", LastName="User", Email="test@example.test", PasswordHash="Password123!"};
    private static DataContext Context() => new(new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private UserService Service(DataContext db) => new(db, new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> {{"AppSettings:Token", SigningKey}}).Build(), email, clock);

    [Fact] public async Task SignupIssuesOnlyAVerificationChallenge()
    {
        using var db = Context();
        await Service(db).SignUp(Signup);
        var user = await db.Users.SingleAsync();
        Assert.False(user.IsVerified);
        Assert.Matches("^[0-9]{6}$", email.Sent.Single().Code);
        Assert.Equal(clock.GetUtcNow().AddMinutes(10), user.VerificationCodeExpiresAt);
        Assert.Equal("verify_email", user.VerificationCodePurpose);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db).CreateToken(user));
    }

    [Fact] public async Task CorrectCodeIssuesOneTokenAndCannotBeReplayed()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        var code = email.Sent.Single().Code;
        var token = await service.VerifyVerificationCode(Signup.Email, code);
        Assert.NotNull(token);
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims, c => c.Value == "User");
        Assert.True((await db.Users.SingleAsync()).IsVerified);
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, code));
    }

    [Fact] public async Task ExpiredCodeCannotVerifyTheAccount()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, email.Sent.Single().Code));
        Assert.False((await db.Users.SingleAsync()).IsVerified);
    }

    [Fact] public async Task FiveWrongGuessesInvalidateTheChallenge()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        var code = email.Sent.Single().Code;
        for (var i = 0; i < 5; i++) Assert.Null(await service.VerifyVerificationCode(Signup.Email, "invalid"));
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, code));
        Assert.False((await db.Users.SingleAsync()).IsVerified);
    }

    [Fact] public async Task ResendingIsThrottledAndReplacesTheOldCodeAfterCooldown()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        var first = email.Sent.Single().Code;
        await service.SendVerificationCodeAsync(Signup.Email);
        Assert.Single(email.Sent);
        clock.Advance(TimeSpan.FromSeconds(61));
        await service.SendVerificationCodeAsync(Signup.Email);
        Assert.Equal(2, email.Sent.Count);
        Assert.NotEqual(first, email.Sent[1].Code);
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, first));
        Assert.NotNull(await service.VerifyVerificationCode(Signup.Email, email.Sent[1].Code));
    }

    [Fact] public async Task ResetCodeCannotBeExchangedForALoginToken()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        var user = await db.Users.SingleAsync(); user.IsVerified = true; await db.SaveChangesAsync();
        clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPassword(Signup.Email);
        var code = email.Sent.Last().Code;
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, code));
        await service.ResetPassword(Signup.Email, code, "Changed123!");
        Assert.NotNull(await service.SignIn(Signup.Email, "Changed123!"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetPassword(Signup.Email, code, "Again123!"));
    }

    [Fact] public async Task VerificationCodeCannotResetThePassword()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetPassword(Signup.Email, email.Sent.Single().Code, "Changed123!"));
        Assert.NotNull(await service.VerifyVerificationCode(Signup.Email, email.Sent.Single().Code));
    }

    [Fact] public async Task VerifiedAccountsCannotRequestVerificationLoginCodes()
    {
        using var db = Context(); var service = Service(db);
        await service.SignUp(Signup);
        await service.VerifyVerificationCode(Signup.Email, email.Sent.Single().Code);
        clock.Advance(TimeSpan.FromMinutes(2));
        await service.SendVerificationCodeAsync(Signup.Email);
        Assert.Single(email.Sent);
    }

    [Fact] public async Task ChallengesWithoutExpiryMetadataAreRejected()
    {
        using var db = Context(); var service = Service(db);
        db.Users.Add(new User {FirstName="Legacy", LastName="User", Email=Signup.Email, PasswordHash="unused", Role="User", VerificationCode="1234"});
        await db.SaveChangesAsync();
        Assert.Null(await service.VerifyVerificationCode(Signup.Email, "1234"));
    }

    [Fact] public async Task ConcurrentConsumersCannotRedeemOneChallengeTwice()
    {
        var options = new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var first = new DataContext(options); using var second = new DataContext(options);
        await Service(first).SignUp(Signup);
        await second.Users.SingleAsync(); // Simulate a request that read the record before another request consumed it.
        var code = email.Sent.Single().Code;
        Assert.NotNull(await Service(first).VerifyVerificationCode(Signup.Email, code));
        Assert.Null(await Service(second).VerifyVerificationCode(Signup.Email, code));
    }

    [Fact] public async Task AuthenticationPreservesTheDatabasesExactEmailIdentity()
    {
        using var db = Context();
        foreach (var pair in new[] {("Test@example.test", "111111"), ("test@example.test", "222222")})
            db.Users.Add(new User {FirstName="Test",LastName="User",Email=pair.Item1,PasswordHash="unused",Role="User",
                VerificationCode=pair.Item2,VerificationCodePurpose="verify_email",VerificationCodeExpiresAt=clock.GetUtcNow().AddMinutes(10)});
        await db.SaveChangesAsync();
        Assert.NotNull(await Service(db).VerifyVerificationCode("test@example.test", "222222"));
        Assert.False((await db.Users.SingleAsync(x => x.Email == "Test@example.test")).IsVerified);
    }
}

internal class ManualClock : TimeProvider
{
    private DateTimeOffset now = new(2026,11,6,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan amount) => now += amount;
}

internal class RecordingEmail : IEmailService
{
    public List<(string Email, string Code)> Sent { get; } = new();
    public Task SendVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken = default) { Sent.Add((toEmail,code)); return Task.CompletedTask; }
    public Task SendMockInterviewSubmittedNotificationAsync(string adminEmail,string name,CancellationToken cancellationToken=default) => Task.CompletedTask;
    public Task SendMockInterviewReportAsync(string toEmail,string name,byte[] bytes,string fileName,CancellationToken cancellationToken=default) => Task.CompletedTask;
}
