using NLog;
using NLog.Web;

namespace FrancProject.Extensions;

public static class LoggingExtensions
{
    public static string GetLogDirectory(IHostEnvironment environment) =>
        Path.Combine(environment.ContentRootPath, "Logs");

    public static WebApplicationBuilder AddFrancFileLogging(this WebApplicationBuilder builder)
    {
        var logDirectory = GetLogDirectory(builder.Environment);
        Directory.CreateDirectory(logDirectory);

        var nlogConfigPath = Path.Combine(builder.Environment.ContentRootPath, "nlog.config");
        if (!File.Exists(nlogConfigPath))
            nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");

        var config = LogManager.Configuration;
        if (config == null || config.AllTargets.Count == 0)
        {
            config = LogManager.Setup()
                .LoadConfigurationFromFile(nlogConfigPath)
                .LogFactory.Configuration;
        }

        config.Variables["minLevel"] = builder.Environment.IsDevelopment() ? "Debug" : "Info";
        config.Variables["logDirectory"] = logDirectory;
        LogManager.Configuration = config;
        LogManager.ReconfigExistingLoggers();

        builder.Logging.ClearProviders();
        builder.Host.UseNLog();

        return builder;
    }

    public static void EnsureLogsDirectoryExists(this WebApplication app)
    {
        Directory.CreateDirectory(GetLogDirectory(app.Environment));
    }
}
