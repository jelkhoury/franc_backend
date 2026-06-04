using FrancProject.Interfaces;
using FrancProject.Options;
using FrancProject.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FrancProject.Extensions;

public static class UploadSecurityExtensions
{
    public static WebApplicationBuilder AddUploadSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<UploadSecurityOptions>(
            builder.Configuration.GetSection(UploadSecurityOptions.SectionName));

        var uploadOptions = builder.Configuration
            .GetSection(UploadSecurityOptions.SectionName)
            .Get<UploadSecurityOptions>() ?? new UploadSecurityOptions();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = uploadOptions.MaxRequestBodySizeBytes;
        });

        builder.Services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = uploadOptions.MaxRequestBodySizeBytes;
        });

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = uploadOptions.MaxRequestBodySizeBytes;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = 32_768;
        });

        builder.Services.AddScoped<IFileUploadSecurityService, FileUploadSecurityService>();

        return builder;
    }
}
