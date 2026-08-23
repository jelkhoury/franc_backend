using FrancProject.Data;
using FrancProject.Dto.AdminAnalytics;
using FrancProject.Helpers;
using FrancProject.Interfaces;
using FrancProject.Models;
using FrancProject.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FrancProject.Services.AdminAnalytics;

public partial class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly DataContext _context;
    private readonly IMemoryCache _cache;
    private readonly AnalyticsOptions _options;

    public AdminAnalyticsService(
        DataContext context,
        IMemoryCache cache,
        IOptions<AnalyticsOptions> analyticsOptions)
    {
        _context = context;
        _cache = cache;
        _options = analyticsOptions.Value;
    }

    private Task<List<AnalyticsActivityRecord>> LoadActivitiesAsync(AnalyticsQueryDto query) =>
        AnalyticsActivityLoader.LoadAllAsync(_context, query, _options.UseActivityEvents);

    public Task<AnalyticsOverviewDto> GetOverviewAsync(AnalyticsQueryDto query) =>
        WithCache("overview", query, () => GetOverviewCoreAsync(query));

    private async Task<AnalyticsOverviewDto> GetOverviewCoreAsync(AnalyticsQueryDto query)
    {
        var totalUsers = await _context.Users.AsNoTracking().CountAsync();
        var rows = await AnalyticsOverviewAggregator.LoadAsync(_context, query, _options.UseActivityEvents);
        if (!string.IsNullOrWhiteSpace(query.ServiceKey))
        {
            var serviceKey = query.ServiceKey.Trim();
            rows = rows.Where(r => r.ServiceKey == serviceKey).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var wantCompleted = query.Status.Trim()
                .Equals("completed", StringComparison.OrdinalIgnoreCase)
                || query.Status.Trim().Equals("evaluated", StringComparison.OrdinalIgnoreCase)
                || query.Status.Trim().Equals("uploaded", StringComparison.OrdinalIgnoreCase);
            rows = rows.Where(r => r.IsCompleted == wantCompleted).ToList();
        }

        var activeUsers = rows.Select(a => a.UserId).Distinct().Count();
        var totalActivities = rows.Count;
        var completedActivities = rows.Count(a => a.IsCompleted);

        var serviceGroups = rows
            .GroupBy(a => a.ServiceKey)
            .Select(g => new ServiceUsageSummaryDto
            {
                ServiceKey = g.Key,
                ServiceName = AnalyticsConstants.GetServiceName(g.Key),
                UniqueUsers = g.Select(x => x.UserId).Distinct().Count(),
                Activities = g.Count(),
                Completed = g.Count(x => x.IsCompleted),
                CompletionRate = g.Any()
                    ? Math.Round(100.0 * g.Count(x => x.IsCompleted) / g.Count(), 1)
                    : 0
            })
            .OrderByDescending(s => s.Activities)
            .ToList();

        foreach (var service in serviceGroups)
        {
            service.ActivitySharePercent = totalActivities > 0
                ? Math.Round(100.0 * service.Activities / totalActivities, 1)
                : 0;
        }

        var top = serviceGroups.FirstOrDefault();

        return new AnalyticsOverviewDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalActivities = totalActivities,
            CompletedActivities = completedActivities,
            CompletionRate = totalActivities > 0
                ? Math.Round(100.0 * completedActivities / totalActivities, 1)
                : 0,
            MostUsedService = top == null ? null : new MostUsedServiceDto
            {
                ServiceKey = top.ServiceKey,
                ServiceName = top.ServiceName,
                ActivityCount = top.Activities
            },
            ServiceUsage = serviceGroups
        };
    }

    public Task<List<ActivityTrendPointDto>> GetActivityTrendAsync(AnalyticsQueryDto query) =>
        WithCache("activity-trend", query, () => GetActivityTrendCoreAsync(query));

    private async Task<List<ActivityTrendPointDto>> GetActivityTrendCoreAsync(AnalyticsQueryDto query)
    {
        var activities = await LoadActivitiesAsync(query);
        var groupBy = (query.GroupBy ?? "daily").Trim().ToLowerInvariant();

        var grouped = activities
            .GroupBy(a => GetTrendBucket(a.OccurredAt, groupBy))
            .OrderBy(g => g.Key)
            .Select(g => new ActivityTrendPointDto
            {
                Date = g.Key,
                Label = FormatTrendLabel(g.Key, groupBy),
                ActiveUsers = g.Select(x => x.UserId).Distinct().Count(),
                Activities = g.Count(),
                Completed = g.Count(x => x.IsCompleted)
            })
            .ToList();

        return grouped;
    }

    public Task<PagedResultDto<RecentActivityItemDto>> GetRecentActivityAsync(AnalyticsQueryDto query) =>
        WithCache("recent-activity", query, () => GetRecentActivityCoreAsync(query));

    private async Task<PagedResultDto<RecentActivityItemDto>> GetRecentActivityCoreAsync(AnalyticsQueryDto query)
    {
        var activities = await LoadActivitiesAsync(query);

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var sorted = activities
                .OrderByDescending(a => a.OccurredAt)
                .ToList();
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var pageItems = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var items = await MapRecentActivityItemsAsync(pageItems, query, skipSearch: true);
            var total = sorted.Count;
            return new PagedResultDto<RecentActivityItemDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
                Items = items
            };
        }

        var allItems = await MapRecentActivityItemsAsync(activities, query);
        allItems = allItems.OrderByDescending(i => i.OccurredAt).ToList();
        return Paginate(allItems, query.Page, query.PageSize);
    }

    private async Task<List<RecentActivityItemDto>> MapRecentActivityItemsAsync(
        List<AnalyticsActivityRecord> activities,
        AnalyticsQueryDto query,
        bool skipSearch = false)
    {
        var userLookup = await BuildUserLookupAsync(activities.Select(a => a.UserId));

        var items = activities
            .Select(a =>
            {
                var hasUser = userLookup.TryGetValue(a.UserId, out var user);
                return new RecentActivityItemDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = hasUser ? user.FullName : $"User {a.UserId}",
                    UserEmail = hasUser ? user.Email : string.Empty,
                    ActivityLabel = a.ActivityLabel,
                    ServiceKey = a.ServiceKey,
                    ServiceName = AnalyticsConstants.GetServiceName(a.ServiceKey),
                    Result = a.Result,
                    Status = a.Status,
                    OccurredAt = a.OccurredAt
                };
            })
            .ToList();

        if (!skipSearch && !string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(i =>
                i.UserName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.ActivityLabel.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (i.Result?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return items;
    }

    public Task<object> GetServiceAnalyticsAsync(string serviceKey, AnalyticsQueryDto query)
    {
        if (!AnalyticsConstants.IsValidServiceKey(serviceKey))
            throw new ArgumentException($"Unknown service key: {serviceKey}");

        return WithCache("service", query, () => GetServiceAnalyticsCoreAsync(serviceKey, query), serviceKey);
    }

    private async Task<object> GetServiceAnalyticsCoreAsync(string serviceKey, AnalyticsQueryDto query)
    {
        return serviceKey switch
        {
            AnalyticsConstants.Sds => await GetSdsAnalyticsAsync(query),
            AnalyticsConstants.MockInterview => await GetMockInterviewAnalyticsAsync(query),
            AnalyticsConstants.JobComparison => await GetJobComparisonAnalyticsAsync(query),
            AnalyticsConstants.Gamification => await GetGamificationAnalyticsAsync(query),
            AnalyticsConstants.Resume => await GetFileAnalyticsAsync(query, "Resume"),
            AnalyticsConstants.CoverLetter => await GetFileAnalyticsAsync(query, "CoverLetter"),
            AnalyticsConstants.Chat => await GetChatAnalyticsAsync(query),
            AnalyticsConstants.JobMatching => await GetJobMatchingAnalyticsAsync(query),
            _ => throw new ArgumentException($"Unknown service key: {serviceKey}")
        };
    }

    public Task<PagedResultDto<UserActivitySummaryDto>> GetUsersAsync(AnalyticsQueryDto query) =>
        WithCache("users", query, () => GetUsersCoreAsync(query));

    private async Task<PagedResultDto<UserActivitySummaryDto>> GetUsersCoreAsync(AnalyticsQueryDto query)
    {
        var activities = await LoadActivitiesAsync(query);
        var activityByUser = activities
            .GroupBy(a => a.UserId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Services = g.Select(x => x.ServiceKey).Distinct().ToList(),
                    Total = g.Count(),
                    Completed = g.Count(x => x.IsCompleted),
                    LastAt = g.Max(x => x.OccurredAt)
                });

        var users = await _context.Users.AsNoTracking().ToListAsync();

        var summaries = users.Select(u =>
        {
            activityByUser.TryGetValue(u.Id, out var stats);
            return new UserActivitySummaryDto
            {
                UserId = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Faculty = u.Faculty,
                Major = u.Major,
                RegisteredAt = u.CreatedAt,
                ServicesUsed = stats?.Services ?? [],
                TotalActivities = stats?.Total ?? 0,
                CompletedActivities = stats?.Completed ?? 0,
                LastActivityAt = stats?.LastAt
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            summaries = summaries.Where(u =>
                u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        summaries = ApplyUserSort(summaries, query.SortBy, query.SortDir);
        return Paginate(summaries, query.Page, query.PageSize);
    }

    public Task<User360Dto?> GetUser360Async(int userId, AnalyticsQueryDto query) =>
        WithCache("user360", query, () => GetUser360CoreAsync(userId, query), userId: userId);

    private async Task<User360Dto?> GetUser360CoreAsync(int userId, AnalyticsQueryDto query)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        query.UserId = userId;
        var activities = await LoadActivitiesAsync(query);
        var recentItems = await MapRecentActivityItemsAsync(activities, query);

        var serviceCounts = activities
            .GroupBy(a => a.ServiceKey)
            .ToDictionary(g => g.Key, g => g.Count());

        var mostUsed = serviceCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();

        var dto = new User360Dto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Faculty = user.Faculty,
            Major = user.Major,
            RegisteredAt = user.CreatedAt,
            LastActivityAt = activities.Count > 0 ? activities.Max(a => a.OccurredAt) : null,
            Summary = new User360SummaryDto
            {
                ServicesUsed = serviceCounts.Count,
                TotalActivities = activities.Count,
                CompletedActivities = activities.Count(a => a.IsCompleted),
                MostUsedServiceKey = mostUsed.Key,
                MostUsedServiceName = mostUsed.Key == null
                    ? null
                    : AnalyticsConstants.GetServiceName(mostUsed.Key),
                LastActiveAt = activities.Count > 0 ? activities.Max(a => a.OccurredAt) : null
            },
            RecentActivity = Paginate(recentItems, 1, 10).Items,
            Services = new User360ServicesDto
            {
                MockInterview = await BuildUserMockInterviewAsync(userId, query),
                Sds = await BuildUserSdsAsync(userId, query),
                JobComparison = await BuildUserJobComparisonAsync(userId, query),
                Gamification = await BuildUserGamificationAsync(userId),
                Resume = await BuildUserFilesAsync(userId, "Resume"),
                CoverLetter = await BuildUserFilesAsync(userId, "CoverLetter"),
                Chat = await BuildUserChatAsync(userId, query),
                JobMatching = await BuildUserJobMatchingAsync(userId, query)
            }
        };

        return dto;
    }

    private async Task<User360JobMatchingDto?> BuildUserJobMatchingAsync(int userId, AnalyticsQueryDto query)
    {
        var searches = await _context.JobMatchingSearches
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (searches.Count == 0)
            return null;

        searches = FilterByDate(searches, query, s => s.SearchedAt.UtcDateTime);

        return new User360JobMatchingDto
        {
            Searches = searches.Count,
            Items = searches
                .OrderByDescending(s => s.SearchedAt)
                .Select(s => new User360JobMatchingItemDto
                {
                    Id = s.Id,
                    SearchType = s.SearchType,
                    Faculty = s.Faculty,
                    Major = s.Major,
                    Country = s.Country,
                    QueryText = s.QueryText,
                    ResultsCount = s.ResultsCount,
                    SearchedAt = s.SearchedAt
                })
                .ToList()
        };
    }

    private async Task<User360MockInterviewDto?> BuildUserMockInterviewAsync(int userId, AnalyticsQueryDto query)
    {
        var answers = await _context.Answers
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.MockInterviewId != null)
            .Include(a => a.Question).ThenInclude(q => q.Major)
            .Include(a => a.MockInterview)
            .ToListAsync();

        if (answers.Count == 0)
            return null;

        answers = FilterByDate(answers, query, a => a.CreatedAt);

        var reports = await _context.EvaluationReports
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var items = answers
            .GroupBy(a => a.MockInterviewId)
            .Select(g =>
            {
                var first = g.OrderByDescending(x => x.CreatedAt).First();
                var report = reports.FirstOrDefault(r => g.Any(x => x.EvaluationReportId == r.Id));
                return new User360MockInterviewItemDto
                {
                    Id = first.MockInterviewId!.Value,
                    Status = first.MockInterview!.IsEvaluated ? "evaluated" : "submitted",
                    OverallRating = report?.OverallRating,
                    Major = first.Question?.Major?.Name,
                    SubmittedAt = g.Max(x => x.CreatedAt),
                    ReportGenerated = report != null
                };
            })
            .OrderByDescending(i => i.SubmittedAt)
            .ToList();

        var ratings = items.Where(i => i.OverallRating.HasValue).Select(i => i.OverallRating!.Value).ToList();

        return new User360MockInterviewDto
        {
            Interviews = items.Count,
            Attempts = items.Count,
            Evaluated = items.Count(i => i.Status == "evaluated"),
            AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
            ReportsGenerated = items.Count(i => i.ReportGenerated),
            Items = items
        };
    }

    private async Task<User360SdsDto?> BuildUserSdsAsync(int userId, AnalyticsQueryDto query)
    {
        var results = await _context.SDSResults
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();

        if (results.Count == 0)
            return null;

        results = FilterByDate(results, query, r => r.IsCompleted && r.CompletedAt.HasValue ? r.CompletedAt.Value : r.CreatedAt);

        var completed = results.Where(r => r.IsCompleted).ToList();

        return new User360SdsDto
        {
            Attempts = results.Count,
            Completed = completed.Count,
            HollandCode = completed.OrderByDescending(r => r.CompletedAt).FirstOrDefault()?.HollandCode,
            Items = results
                .OrderByDescending(r => r.CompletedAt ?? r.CreatedAt)
                .Select(r => new User360SdsItemDto
                {
                    Id = r.Id,
                    HollandCode = r.HollandCode,
                    AttemptNumber = r.AttemptNumber,
                    Status = r.IsCompleted ? "completed" : "draft",
                    CompletedAt = r.CompletedAt
                })
                .ToList()
        };
    }

    private async Task<User360JobComparisonDto?> BuildUserJobComparisonAsync(int userId, AnalyticsQueryDto query)
    {
        var comparisons = await _context.JobComparisons
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .Include(j => j.Answers)
            .ToListAsync();

        if (comparisons.Count == 0)
            return null;

        comparisons = FilterByDate(comparisons, query, j => j.CreatedAt);
        var criteria = await _context.JobComparisonCriteria.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Category);

        var items = comparisons
            .Select(j =>
            {
                var computed = ComputeComparisonAnalytics(j, criteria);
                return new User360JobComparisonItemDto
                {
                    Id = j.Id,
                    JobAName = j.JobAName,
                    JobBName = j.JobBName,
                    ScoreA = Math.Round(computed.ScoreA, 1),
                    ScoreB = Math.Round(computed.ScoreB, 1),
                    Winner = computed.Winner,
                    Status = j.IsCompleted ? "completed" : "draft",
                    CreatedAt = j.CreatedAt
                };
            })
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        return new User360JobComparisonDto
        {
            Comparisons = items.Count,
            Completed = items.Count(i => i.Status == "completed"),
            Items = items
        };
    }

    private async Task<User360GamificationDto?> BuildUserGamificationAsync(int userId)
    {
        var progress = await _context.UserGameProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var sessions = await _context.GameSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Answers)
            .ToListAsync();

        if (progress == null && sessions.Count == 0)
            return null;

        var answers = sessions.SelectMany(s => s.Answers).ToList();
        var correct = answers.Count(a => a.IsCorrect);
        var total = answers.Count;

        var badges = new List<string>();
        if (progress != null)
        {
            if (progress.BronzeBadgeEarned) badges.Add("Bronze");
            if (progress.SilverBadgeEarned) badges.Add("Silver");
            if (progress.GoldBadgeEarned) badges.Add("Gold");
            if (progress.PlatinumBadgeEarned) badges.Add("Platinum");
            if (progress.DiamondBadgeEarned) badges.Add("Diamond");
        }

        return new User360GamificationDto
        {
            CurrentLevel = progress?.CurrentLevel ?? 1,
            TotalPoints = progress?.TotalPoints ?? 0,
            Accuracy = total > 0 ? Math.Round(100.0 * correct / total, 1) : 0,
            SessionsCompleted = sessions.Count(s => s.Status == GameQuizConstants.StatusCompleted),
            BadgesEarned = badges,
            AbilitiesUsed = new Dictionary<string, int>
            {
                ["Skip"] = answers.Count(a => a.UsedSkip),
                ["FiftyFifty"] = answers.Count(a => a.UsedFiftyFifty),
                ["DoubleChance"] = answers.Count(a => a.UsedDoubleChance),
                ["TimeFreeze"] = answers.Count(a => a.UsedTimeFreeze)
            }
        };
    }

    private async Task<User360FilesDto?> BuildUserFilesAsync(int userId, string title)
    {
        var files = await _context.Files
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Title == title)
            .ToListAsync();

        if (files.Count == 0)
            return null;

        return new User360FilesDto
        {
            Uploads = files.Count,
            Items = files
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new User360FileItemDto
                {
                    Id = f.Id,
                    FileName = title == "Resume"
                        ? (Path.GetFileName(f.ResumeUrl ?? "resume.pdf") ?? "resume.pdf")
                        : (Path.GetFileName(f.CoverUrl ?? "cover-letter.pdf") ?? "cover-letter.pdf"),
                    UploadedAt = f.CreatedAt
                })
                .ToList()
        };
    }

    private async Task<User360ChatDto?> BuildUserChatAsync(int userId, AnalyticsQueryDto query)
    {
        var messages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync();

        if (messages.Count == 0)
            return null;

        messages = FilterByDate(messages, query, m => m.CreatedAt);

        var sessions = messages
            .GroupBy(m => m.SessionId)
            .Select(g => new User360ChatItemDto
            {
                Id = g.Key,
                MessageCount = g.Count(),
                LastMessageAt = g.Max(m => m.CreatedAt)
            })
            .OrderByDescending(s => s.LastMessageAt)
            .ToList();

        return new User360ChatDto
        {
            Sessions = sessions.Count,
            TotalMessages = messages.Count,
            Items = sessions
        };
    }

    private sealed class ComparisonComputed
    {
        public int Id { get; init; }
        public string UserName { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string JobAName { get; init; } = null!;
        public string JobBName { get; init; } = null!;
        public double ScoreA { get; init; }
        public double ScoreB { get; init; }
        public string Winner { get; init; } = null!;
        public bool IsCompleted { get; init; }
        public DateTime CreatedAt { get; init; }
        public List<CategoryWinnerRow> CategoryWinners { get; init; } = new();
    }

    private sealed class CategoryWinnerRow
    {
        public string Category { get; init; } = null!;
        public string Winner { get; init; } = null!;
    }

    private static ComparisonComputed ComputeComparisonAnalytics(
        JobComparison comparison,
        Dictionary<int, string> criteria)
    {
        var answerRows = comparison.Answers.Select(a => new JobComparisonScoreHelper.AnswerRow(
            a.ScoreA,
            a.ScoreB,
            a.Weight,
            a.NotApplicableA,
            a.NotApplicableB,
            criteria.GetValueOrDefault(a.CriterionId, "HEAD"))).ToList();

        var (scoreA, scoreB, winner) = JobComparisonScoreHelper.ComputeComparisonResult(answerRows);

        var categoryWinners = answerRows
            .GroupBy(a => a.Category)
            .Select(g =>
            {
                var (catA, catB) = JobComparisonScoreHelper.ComputeWeightedScores(g);
                return new CategoryWinnerRow
                {
                    Category = g.Key,
                    Winner = JobComparisonScoreHelper.DetermineWinner(catA, catB)
                };
            })
            .ToList();

        return new ComparisonComputed
        {
            Id = comparison.Id,
            UserName = comparison.User == null
                ? $"User {comparison.UserId}"
                : $"{comparison.User.FirstName} {comparison.User.LastName}",
            Email = comparison.User?.Email ?? string.Empty,
            JobAName = comparison.JobAName,
            JobBName = comparison.JobBName,
            ScoreA = scoreA,
            ScoreB = scoreB,
            Winner = winner,
            IsCompleted = comparison.IsCompleted,
            CreatedAt = comparison.CreatedAt,
            CategoryWinners = categoryWinners
        };
    }

    private async Task<Dictionary<int, (string FullName, string Email)>> BuildUserLookupAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, (string, string)>();

        return await _context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => (u.FullName, u.Email));
    }

    private static List<T> FilterByDate<T>(List<T> items, AnalyticsQueryDto query, Func<T, DateTime> selector)
    {
        IEnumerable<T> filtered = items;
        if (query.FromDate.HasValue)
            filtered = filtered.Where(i => selector(i) >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            filtered = filtered.Where(i => selector(i) <= query.ToDate.Value);
        return filtered.ToList();
    }

    private static List<UserActivitySummaryDto> ApplyUserSort(
        List<UserActivitySummaryDto> items,
        string? sortBy,
        string sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "lastActivityAt").ToLowerInvariant() switch
        {
            "firstname" => desc
                ? items.OrderByDescending(u => u.FirstName).ToList()
                : items.OrderBy(u => u.FirstName).ToList(),
            "email" => desc
                ? items.OrderByDescending(u => u.Email).ToList()
                : items.OrderBy(u => u.Email).ToList(),
            "totalactivities" => desc
                ? items.OrderByDescending(u => u.TotalActivities).ToList()
                : items.OrderBy(u => u.TotalActivities).ToList(),
            _ => desc
                ? items.OrderByDescending(u => u.LastActivityAt ?? DateTime.MinValue).ToList()
                : items.OrderBy(u => u.LastActivityAt ?? DateTime.MinValue).ToList()
        };
    }

    private static PagedResultDto<T> Paginate<T>(List<T> items, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = items.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResultDto<T>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            Items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    private static DateTime GetTrendBucket(DateTime date, string groupBy) => groupBy switch
    {
        "weekly" => date.Date.AddDays(-(int)date.DayOfWeek),
        "monthly" => new DateTime(date.Year, date.Month, 1),
        _ => date.Date
    };

    private static string FormatTrendLabel(DateTime date, string groupBy) => groupBy switch
    {
        "weekly" => $"Week of {date:MMM d}",
        "monthly" => date.ToString("MMM yyyy"),
        _ => date.ToString("MMM d")
    };

    private async Task<T> WithCache<T>(
        string endpoint,
        AnalyticsQueryDto query,
        Func<Task<T>> load,
        string? serviceKey = null,
        int? userId = null)
    {
        if (!_options.EnableResponseCache)
            return await load();

        var cacheKey = BuildCacheKey(endpoint, query, serviceKey, userId);
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var result = await load();
        var ttl = TimeSpan.FromDays(Math.Max(0.001, _options.CacheDurationDays));
        _cache.Set(cacheKey, result, ttl);
        return result;
    }

    private string BuildCacheKey(string endpoint, AnalyticsQueryDto q, string? serviceKey, int? userId)
    {
        var queryKey = string.Join('|',
            q.FromDate?.ToString("o") ?? "",
            q.ToDate?.ToString("o") ?? "",
            q.ServiceKey ?? "",
            q.UserId?.ToString() ?? "",
            q.Status ?? "",
            q.Search ?? "",
            q.Page.ToString(),
            q.PageSize.ToString(),
            q.SortBy ?? "",
            q.SortDir ?? "",
            q.GroupBy ?? "");

        return $"admin-analytics:{endpoint}:{serviceKey ?? ""}:{userId?.ToString() ?? ""}:{queryKey}:events={_options.UseActivityEvents}";
    }
}
