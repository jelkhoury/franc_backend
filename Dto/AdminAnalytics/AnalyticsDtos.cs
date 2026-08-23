namespace FrancProject.Dto.AdminAnalytics
{
    public class AnalyticsQueryDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? ServiceKey { get; set; }
        public int? UserId { get; set; }
        public string? Status { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public string SortDir { get; set; } = "desc";
        public string GroupBy { get; set; } = "daily";
    }

    public class ServiceUsageSummaryDto
    {
        public string ServiceKey { get; set; } = null!;
        public string ServiceName { get; set; } = null!;
        public int UniqueUsers { get; set; }
        public int Activities { get; set; }
        public int Completed { get; set; }
        public double CompletionRate { get; set; }
        public double ActivitySharePercent { get; set; }
    }

    public class MostUsedServiceDto
    {
        public string ServiceKey { get; set; } = null!;
        public string ServiceName { get; set; } = null!;
        public int ActivityCount { get; set; }
    }

    public class AnalyticsOverviewDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalActivities { get; set; }
        public int CompletedActivities { get; set; }
        public double CompletionRate { get; set; }
        public MostUsedServiceDto? MostUsedService { get; set; }
        public List<ServiceUsageSummaryDto> ServiceUsage { get; set; } = new();
    }

    public class ActivityTrendPointDto
    {
        public DateTime Date { get; set; }
        public string Label { get; set; } = null!;
        public int ActiveUsers { get; set; }
        public int Activities { get; set; }
        public int Completed { get; set; }
    }

    public class RecentActivityItemDto
    {
        public string Id { get; set; } = null!;
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
        public string ActivityLabel { get; set; } = null!;
        public string ServiceKey { get; set; } = null!;
        public string ServiceName { get; set; } = null!;
        public string? Result { get; set; }
        public string Status { get; set; } = null!;
        public DateTime OccurredAt { get; set; }
    }

    public class PagedResultDto<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public List<T> Items { get; set; } = new();
    }

    public class UserActivitySummaryDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Faculty { get; set; }
        public string? Major { get; set; }
        public List<string> ServicesUsed { get; set; } = new();
        public int TotalActivities { get; set; }
        public int CompletedActivities { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime? RegisteredAt { get; set; }
    }

    public class User360SummaryDto
    {
        public int ServicesUsed { get; set; }
        public int TotalActivities { get; set; }
        public int CompletedActivities { get; set; }
        public string? MostUsedServiceKey { get; set; }
        public string? MostUsedServiceName { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }

    public class User360MockInterviewItemDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = null!;
        public float? OverallRating { get; set; }
        public string? Major { get; set; }
        public DateTime SubmittedAt { get; set; }
        public bool ReportGenerated { get; set; }
    }

    public class User360MockInterviewDto
    {
        public int Interviews { get; set; }
        public int Attempts { get; set; }
        public int Evaluated { get; set; }
        public double? AverageRating { get; set; }
        public int ReportsGenerated { get; set; }
        public List<User360MockInterviewItemDto> Items { get; set; } = new();
    }

    public class User360SdsItemDto
    {
        public int Id { get; set; }
        public string? HollandCode { get; set; }
        public int AttemptNumber { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? CompletedAt { get; set; }
    }

    public class User360SdsDto
    {
        public int Attempts { get; set; }
        public int Completed { get; set; }
        public string? HollandCode { get; set; }
        public List<User360SdsItemDto> Items { get; set; } = new();
    }

    public class User360JobComparisonItemDto
    {
        public int Id { get; set; }
        public string JobAName { get; set; } = null!;
        public string JobBName { get; set; } = null!;
        public double ScoreA { get; set; }
        public double ScoreB { get; set; }
        public string Winner { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class User360JobComparisonDto
    {
        public int Comparisons { get; set; }
        public int Completed { get; set; }
        public List<User360JobComparisonItemDto> Items { get; set; } = new();
    }

    public class User360GamificationDto
    {
        public int CurrentLevel { get; set; }
        public int TotalPoints { get; set; }
        public double Accuracy { get; set; }
        public int SessionsCompleted { get; set; }
        public List<string> BadgesEarned { get; set; } = new();
        public Dictionary<string, int> AbilitiesUsed { get; set; } = new();
    }

    public class User360FileItemDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }

    public class User360FilesDto
    {
        public int Uploads { get; set; }
        public List<User360FileItemDto> Items { get; set; } = new();
    }

    public class User360ChatItemDto
    {
        public string Id { get; set; } = null!;
        public int MessageCount { get; set; }
        public DateTime LastMessageAt { get; set; }
    }

    public class User360ChatDto
    {
        public int Sessions { get; set; }
        public int TotalMessages { get; set; }
        public List<User360ChatItemDto> Items { get; set; } = new();
    }

    public class User360ServicesDto
    {
        public User360MockInterviewDto? MockInterview { get; set; }
        public User360SdsDto? Sds { get; set; }
        public User360JobComparisonDto? JobComparison { get; set; }
        public User360GamificationDto? Gamification { get; set; }
        public User360FilesDto? Resume { get; set; }
        public User360FilesDto? CoverLetter { get; set; }
        public User360ChatDto? Chat { get; set; }
        public User360JobMatchingDto? JobMatching { get; set; }
    }

    public class User360Dto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Faculty { get; set; }
        public string? Major { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public User360SummaryDto Summary { get; set; } = new();
        public List<RecentActivityItemDto> RecentActivity { get; set; } = new();
        public User360ServicesDto Services { get; set; } = new();
    }

    public class LabelCountDto
    {
        public string Label { get; set; } = null!;
        public int Count { get; set; }
    }

    public class HollandCodeCountDto
    {
        public string HollandCode { get; set; } = null!;
        public int Count { get; set; }
    }

    public class AttemptCountDto
    {
        public int AttemptNumber { get; set; }
        public int Count { get; set; }
    }

    public class SdsServiceAnalyticsDto
    {
        public int Started { get; set; }
        public int Completed { get; set; }
        public int Drafts { get; set; }
        public double CompletionRate { get; set; }
        public int UniqueUsers { get; set; }
        public List<HollandCodeCountDto> ResultDistribution { get; set; } = new();
        public List<AttemptCountDto> AttemptDistribution { get; set; } = new();
        public string? MostCommonHollandCode { get; set; }
        public List<SdsRecentCompletionDto> RecentCompletions { get; set; } = new();
    }

    public class SdsRecentCompletionDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? HollandCode { get; set; }
        public int AttemptNumber { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class MockInterviewRecentDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Major { get; set; }
        public string Status { get; set; } = null!;
        public float? OverallRating { get; set; }
        public bool ReportGenerated { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class QuestionPerformanceDto
    {
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; } = null!;
        public double AverageRating { get; set; }
        public int AnswerCount { get; set; }
    }

    public class MockInterviewServiceAnalyticsDto
    {
        public int Started { get; set; }
        public int Completed { get; set; }
        public int Evaluated { get; set; }
        public int ReportsGenerated { get; set; }
        public int UniqueUsers { get; set; }
        public double? AverageOverallRating { get; set; }
        public List<LabelCountDto> EvaluationDistribution { get; set; } = new();
        public List<LabelCountDto> AttemptDistribution { get; set; } = new();
        public List<QuestionPerformanceDto> QuestionPerformance { get; set; } = new();
        public List<MockInterviewRecentDto> RecentInterviews { get; set; } = new();
    }

    public class JobNameCountDto
    {
        public string JobName { get; set; } = null!;
        public int Count { get; set; }
    }

    public class JobPairCountDto
    {
        public string JobA { get; set; } = null!;
        public string JobB { get; set; } = null!;
        public int Count { get; set; }
    }

    public class WinnerCountDto
    {
        public string Winner { get; set; } = null!;
        public int Count { get; set; }
    }

    public class HeadVsHeartCategoryDto
    {
        public string Category { get; set; } = null!;
        public int JobAWins { get; set; }
        public int JobBWins { get; set; }
        public int Ties { get; set; }
    }

    public class JobComparisonRecentDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string JobAName { get; set; } = null!;
        public string JobBName { get; set; } = null!;
        public double ScoreA { get; set; }
        public double ScoreB { get; set; }
        public string Winner { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class JobComparisonServiceAnalyticsDto
    {
        public int TotalComparisons { get; set; }
        public int CompletedComparisons { get; set; }
        public int DraftComparisons { get; set; }
        public int UniqueUsers { get; set; }
        public double CompletionRate { get; set; }
        public List<JobNameCountDto> MostComparedJobs { get; set; } = new();
        public List<JobPairCountDto> TopJobPairs { get; set; } = new();
        public List<WinnerCountDto> WinnerDistribution { get; set; } = new();
        public List<HeadVsHeartCategoryDto> HeadVsHeart { get; set; } = new();
        public List<JobComparisonRecentDto> RecentComparisons { get; set; } = new();
    }

    public class LevelProgressionDto
    {
        public int LevelNumber { get; set; }
        public string LevelName { get; set; } = null!;
        public int Started { get; set; }
        public int Completed { get; set; }
        public double DropOffRate { get; set; }
    }

    public class AbilityUsageDto
    {
        public string Ability { get; set; } = null!;
        public int UsageCount { get; set; }
    }

    public class QuestionDifficultyDto
    {
        public long QuestionId { get; set; }
        public string QuestionText { get; set; } = null!;
        public double IncorrectRate { get; set; }
        public double CorrectRate { get; set; }
        public int TimesAnswered { get; set; }
    }

    public class GamificationServiceAnalyticsDto
    {
        public int TotalPlayers { get; set; }
        public int GameSessions { get; set; }
        public int QuestionsAnswered { get; set; }
        public int CorrectAnswers { get; set; }
        public int IncorrectAnswers { get; set; }
        public double AccuracyRate { get; set; }
        public double AverageScore { get; set; }
        public int TimeoutCount { get; set; }
        public List<LevelProgressionDto> LevelProgression { get; set; } = new();
        public List<AbilityUsageDto> AbilityUsage { get; set; } = new();
        public List<QuestionDifficultyDto> HardestQuestions { get; set; } = new();
        public List<QuestionDifficultyDto> EasiestQuestions { get; set; } = new();
    }

    public class FileUploadRecentDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }

    public class FileServiceAnalyticsDto
    {
        public int Uploads { get; set; }
        public int UniqueUsers { get; set; }
        public List<FileUploadRecentDto> RecentUploads { get; set; } = new();
    }

    public class ChatSessionRecentDto
    {
        public string Id { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int MessageCount { get; set; }
        public DateTime LastMessageAt { get; set; }
    }

    public class ChatServiceAnalyticsDto
    {
        public int TotalSessions { get; set; }
        public int UniqueUsers { get; set; }
        public int TotalMessages { get; set; }
        public double AverageMessagesPerSession { get; set; }
        public List<ChatSessionRecentDto> RecentSessions { get; set; } = new();
    }

    public class MajorCountDto
    {
        public string Major { get; set; } = null!;
        public int Count { get; set; }
    }

    public class CountryCountDto
    {
        public string Country { get; set; } = null!;
        public int Count { get; set; }
    }

    public class User360JobMatchingItemDto
    {
        public long Id { get; set; }
        public string SearchType { get; set; } = null!;
        public string? Faculty { get; set; }
        public string? Major { get; set; }
        public string? Country { get; set; }
        public string? QueryText { get; set; }
        public int ResultsCount { get; set; }
        public DateTimeOffset SearchedAt { get; set; }
    }

    public class User360JobMatchingDto
    {
        public int Searches { get; set; }
        public List<User360JobMatchingItemDto> Items { get; set; } = new();
    }

    public class JobMatchingRecentSearchDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Major { get; set; }
        public string? Country { get; set; }
        public int ResultsCount { get; set; }
        public DateTimeOffset SearchedAt { get; set; }
    }

    public class JobMatchingServiceAnalyticsDto
    {
        public int TotalSearches { get; set; }
        public int UniqueUsers { get; set; }
        public List<MajorCountDto> TopMajors { get; set; } = new();
        public List<CountryCountDto> TopCountries { get; set; } = new();
        public List<JobMatchingRecentSearchDto> RecentSearches { get; set; } = new();
        public string? Phase2Note { get; set; }
    }
}
