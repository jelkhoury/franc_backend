using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FrancProject.Data;

// Allows migration generation without starting hosted workers or requiring runtime JWT settings.
public sealed class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var arguments = new ConfigurationBuilder().AddCommandLine(args).Build();
        var environment = arguments["environment"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=franc;Username=postgres";
        return new DataContext(new DbContextOptionsBuilder<DataContext>().UseNpgsql(connection).Options);
    }
}
