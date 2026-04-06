using FrancProject.Data;
using FrancProject.Interface;
using FrancProject.Interfaces;
using FrancProject.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Sentry;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry(options =>
{
    // Prefer Azure App Settings / environment variables, fallback to appsettings.json.
    // Azure App Settings examples:
    // - SENTRY_DSN
    // - Sentry__TracesSampleRate
    // - Sentry__ProfilesSampleRate
    options.Dsn =
        builder.Configuration["SENTRY_DSN"]
        ?? builder.Configuration["Sentry:Dsn"];

    options.Debug = builder.Environment.IsDevelopment();
    options.Environment =
        builder.Configuration["SENTRY_ENVIRONMENT"]
        ?? builder.Configuration["Sentry:Environment"]
        ?? builder.Environment.EnvironmentName;

    options.Release =
        builder.Configuration["SENTRY_RELEASE"]
        ?? builder.Configuration["Sentry:Release"];

    // Error capture
    options.AttachStacktrace = true;

    // Performance (tracing) + optional profiling (set sample rates in config)
    options.TracesSampleRate = builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0.0;
    options.ProfilesSampleRate = builder.Configuration.GetValue<double?>("Sentry:ProfilesSampleRate") ?? 0.0;

    // Safer default: don't send usernames/emails/headers unless you explicitly opt-in.
    options.SendDefaultPii = builder.Configuration.GetValue<bool?>("Sentry:SendDefaultPii") ?? false;
});

// --------------------------------------------------
// DATABASE
// --------------------------------------------------
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// --------------------------------------------------
// DEPENDENCY INJECTION
// --------------------------------------------------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEvaluationRepository, EvaluationRepository>();
builder.Services.AddScoped<ISdsRepository, SdsRepository>();
builder.Services.AddScoped<IJobComparisonRepository, JobComparisonRepository>();
builder.Services.AddScoped<JobComparisonExcelService>();
builder.Services.AddScoped<BlobStorageService>();
builder.Services.AddScoped<IJobSearchRepository, JobSearchRepository>();

builder.Services.AddControllers();

// --------------------------------------------------
// CORS
// --------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});
// --------------------------------------------------
// JWT AUTHENTICATION (AZURE SAFE)
// --------------------------------------------------
var jwtToken =
    builder.Configuration["AppSettings:Token"]
    ?? Environment.GetEnvironmentVariable("AppSettings__Token");

if (string.IsNullOrWhiteSpace(jwtToken))
{
    throw new InvalidOperationException(
        "JWT token is missing. Configure AppSettings__Token in Azure Application Settings."
    );
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtToken));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// --------------------------------------------------
// SWAGGER
// --------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FrancProject API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.OperationFilter<FileUploadOperationFilter>();
});

var app = builder.Build();

// --------------------------------------------------
// LOG ACTIVE DATABASE (DEBUG)
// --------------------------------------------------
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(cs))
{
    var b = new SqlConnectionStringBuilder(cs);
    app.Logger.LogInformation(
        "DB Used at runtime: {Server}/{Database}",
        b.DataSource,
        b.InitialCatalog
    );
}

// --------------------------------------------------
// MIDDLEWARE
// --------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseSentryTracing();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
