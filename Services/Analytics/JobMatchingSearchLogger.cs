using FrancProject.Data;
using FrancProject.Interfaces;
using FrancProject.Models.Analytics;
using Microsoft.Extensions.Logging;

namespace FrancProject.Services.Analytics;

public class JobMatchingSearchLogger : IJobMatchingSearchLogger
{
    private readonly DataContext _context;
    private readonly ILogger<JobMatchingSearchLogger> _logger;

    public JobMatchingSearchLogger(DataContext context, ILogger<JobMatchingSearchLogger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task TryLogOpportunitiesSearchAsync(
        int userId,
        string queryText,
        string? locationUsed,
        int resultsCount)
    {
        try
        {
            _context.JobMatchingSearches.Add(new JobMatchingSearch
            {
                UserId = userId,
                SearchType = "opportunities",
                QueryText = queryText,
                Country = locationUsed,
                ResultsCount = resultsCount,
                SearchedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to log job opportunities search for user {UserId}",
                userId);
        }
    }

    public async Task TryLogMajorSkillsSearchAsync(
        int userId,
        string faculty,
        string major,
        string? country)
    {
        try
        {
            _context.JobMatchingSearches.Add(new JobMatchingSearch
            {
                UserId = userId,
                SearchType = "majorSkills",
                Faculty = faculty,
                Major = major,
                Country = country,
                ResultsCount = 0,
                SearchedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to log major skills search for user {UserId}",
                userId);
        }
    }
}
