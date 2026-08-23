using FrancProject.Models.Analytics;

namespace FrancProject.Interfaces
{
    public interface IActivityEventWriter
    {
        Task TryLogAsync(
            int userId,
            string serviceKey,
            string activityType,
            string status,
            string entityId,
            DateTime occurredAt,
            string? resultSummary = null);
    }

    public interface IJobMatchingSearchLogger
    {
        Task TryLogOpportunitiesSearchAsync(int userId, string queryText, string? locationUsed, int resultsCount);
        Task TryLogMajorSkillsSearchAsync(int userId, string faculty, string major, string? country);
    }
}
