using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FrancProject.Controllers;
using FrancProject.Data;
using FrancProject.Interfaces;
using FrancProject.Middlewares;
using FrancProject.Models;
using FrancProject.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FrancProject.Tests;

public class AccountEndpointTests : IClassFixture<AccountApi>
{
    private readonly AccountApi api;
    public AccountEndpointTests(AccountApi api) => this.api = api;

    [Theory]
    [InlineData("GET", "/api/users/get-all-users")]
    [InlineData("POST", "/api/users/add-user")]
    [InlineData("PUT", "/api/users/update-user?id=99")]
    [InlineData("DELETE", "/api/users/delete-user?id=99")]
    [InlineData("POST", "/api/evaluation/evaluate")]
    [InlineData("POST", "/api/evaluation/increase-attempt?userId=99")]
    [InlineData("GET", "/api/blob/get-all-grouped")]
    [InlineData("POST", "/api/blob/create-question")]
    [InlineData("POST", "/api/sds/create-section")]
    [InlineData("GET", "/api/sds/SDSResults")]
    [InlineData("POST", "/api/jobcomparison/criteria/bulk")]
    public async Task OrdinaryUserCannotInvokeAdminActions(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new("Bearer", AccountApi.Token(42));
        var response = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] public async Task AnonymousUserCannotListAccounts()
    {
        var response = await api.Client.GetAsync("/api/users/get-all-users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] public async Task AdminCanListAccounts()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/get-all-users");
        request.Headers.Authorization = new("Bearer", AccountApi.Token(1, "Admin"));
        var response = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(42, "User", 42, 200)]
    [InlineData(42, "User", 99, 401)]
    [InlineData(1, "Admin", 99, 200)]
    public async Task UserQuotaReadEnforcesOwnOrAdminAccess(int caller, string role, int target, int status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/CanUserPerformAction?userId={target}&action=0");
        request.Headers.Authorization = new("Bearer", AccountApi.Token(caller, role));
        var response = await api.Client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 200) {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(target, body.GetProperty("userId").GetInt32());
        }
    }

    [Fact] public async Task CommentedEmailTokenEndpointIsNotRegistered()
    {
        var response = await api.Client.PostAsync("/api/users/generate-token-by-email?email=admin@example.test", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact] public async Task SignupReturnsNoJwtAndSuccessfulVerificationReturnsOne()
    {
        const string email = "signup-http@example.test";
        var response = await api.Client.PostAsJsonAsync("/api/users/signup", new {
            firstName="Test",lastName="User",email,passwordHash="Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("token", out _));
        Assert.True(body.GetProperty("requiresVerification").GetBoolean());
        var code = api.Email.Sent.Single(x => x.Email == email).Code;
        var verified = await api.Client.PostAsync($"/api/users/verify-code?email={email}&code={code}", null);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var token = (await verified.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        var replay = await api.Client.PostAsync($"/api/users/verify-code?email={email}&code={code}", null);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact] public async Task CorrectPasswordForUnverifiedAccountReturnsStableErrorCode()
    {
        const string email = "unverified-http@example.test";
        await api.Client.PostAsJsonAsync("/api/users/signup", new {firstName="Test",lastName="User",email,passwordHash="Password123!"});
        var response = await api.Client.PostAsJsonAsync("/api/users/sign-in", new {email,password="Password123!"});
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("account_not_verified", body.GetProperty("code").GetString());
    }
}

public sealed class AccountApi : IAsyncLifetime
{
    private WebApplication app = null!;
    public HttpClient Client { get; private set; } = null!;
    internal RecordingEmail Email { get; } = new();
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Testing"});
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {{"AppSettings:Token",AccountVerificationTests.SigningKey}});
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(UserController).Assembly);
        // Share one isolated database across request scopes.
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<DataContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IUserService,UserService>();
        builder.Services.AddSingleton<IEmailService>(Email);
        builder.Services.AddSingleton<IFileUploadSecurityService, NoopUploadValidation>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
            o.TokenValidationParameters = new() {
                ValidateIssuer=false, ValidateAudience=false, ValidateIssuerSigningKey=true, ValidateLifetime=true,
                IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccountVerificationTests.SigningKey)), ClockSkew=TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        app = builder.Build();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            foreach (var id in new[] {1,42,99}) db.Users.Add(new User {
                Id=id, FirstName="Test", LastName="User",Email=$"user{id}@example.test",PasswordHash="unused",
                Role=id==1?"Admin":"User",IsVerified=true,MockAttempts=2,CanDoMockInterview=true
            });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient {BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(10)};
    }
    public async Task DisposeAsync() { Client?.Dispose(); if (app != null) { await app.StopAsync(); await app.DisposeAsync(); } }
    internal static string Token(int userId, string role="User") => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims: [new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim(ClaimTypes.Role,role)],
        expires:DateTime.UtcNow.AddMinutes(10), signingCredentials:new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccountVerificationTests.SigningKey)),SecurityAlgorithms.HmacSha256)));
}

internal sealed class NoopUploadValidation : IFileUploadSecurityService
{
    public Task ValidateAsync(IFormFile file,FileUploadCategory category,CancellationToken cancellationToken=default) => Task.CompletedTask;
    public Task ValidateBatchAsync(IEnumerable<IFormFile> files,FileUploadCategory category,long? maxTotalBytes=null,CancellationToken cancellationToken=default) => Task.CompletedTask;
}
