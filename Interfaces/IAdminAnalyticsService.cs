using FrancProject.Dto.AdminAnalytics;

namespace FrancProject.Interfaces
{
    public interface IAdminAnalyticsService
    {
        Task<AnalyticsOverviewDto> GetOverviewAsync(AnalyticsQueryDto query);
        Task<List<ActivityTrendPointDto>> GetActivityTrendAsync(AnalyticsQueryDto query);
        Task<PagedResultDto<RecentActivityItemDto>> GetRecentActivityAsync(AnalyticsQueryDto query);
        Task<object> GetServiceAnalyticsAsync(string serviceKey, AnalyticsQueryDto query);
        Task<PagedResultDto<UserActivitySummaryDto>> GetUsersAsync(AnalyticsQueryDto query);
        Task<User360Dto?> GetUser360Async(int userId, AnalyticsQueryDto query);
    }
}
