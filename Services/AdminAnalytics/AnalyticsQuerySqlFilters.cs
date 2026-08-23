using FrancProject.Dto.AdminAnalytics;
using FrancProject.Helpers;
using FrancProject.Models;
using FrancProject.Models.Analytics;

namespace FrancProject.Services.AdminAnalytics;

internal static class AnalyticsQuerySqlFilters
{
    public static IQueryable<SDSResult> Apply(IQueryable<SDSResult> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(r => r.UserId == query.UserId.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value;
            q = q.Where(r =>
                (r.IsCompleted && r.CompletedAt != null ? r.CompletedAt.Value : r.CreatedAt) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value;
            q = q.Where(r =>
                (r.IsCompleted && r.CompletedAt != null ? r.CompletedAt.Value : r.CreatedAt) <= to);
        }

        return q;
    }

    public static IQueryable<Answer> ApplyMockInterviewAnswers(IQueryable<Answer> q, AnalyticsQueryDto query)
    {
        q = q.Where(a => a.MockInterviewId != null);

        if (query.UserId.HasValue)
            q = q.Where(a => a.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(a => a.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(a => a.CreatedAt <= query.ToDate.Value);

        return q;
    }

    public static IQueryable<EvaluationReport> ApplyMockInterviewReports(
        IQueryable<EvaluationReport> q,
        AnalyticsQueryDto query)
    {
        q = q.Where(r => r.OverallRating != null);

        if (query.UserId.HasValue)
            q = q.Where(r => r.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(r => r.GeneratedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(r => r.GeneratedAt <= query.ToDate.Value);

        return q;
    }

    public static IQueryable<JobComparison> Apply(IQueryable<JobComparison> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(j => j.UserId == query.UserId.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value;
            q = q.Where(j =>
                (j.IsCompleted && j.CompletedAt != null ? j.CompletedAt.Value : j.CreatedAt) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value;
            q = q.Where(j =>
                (j.IsCompleted && j.CompletedAt != null ? j.CompletedAt.Value : j.CreatedAt) <= to);
        }

        return q;
    }

    public static IQueryable<GameSession> Apply(IQueryable<GameSession> q, AnalyticsQueryDto query)
    {
        q = q.Where(s => s.FinishedAt.HasValue || s.Status == GameQuizConstants.StatusInProgress);

        if (query.UserId.HasValue)
            q = q.Where(s => s.UserId == query.UserId.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value;
            q = q.Where(s => (s.FinishedAt ?? s.StartedAt) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value;
            q = q.Where(s => (s.FinishedAt ?? s.StartedAt) <= to);
        }

        return q;
    }

    public static IQueryable<FileRecord> Apply(IQueryable<FileRecord> q, AnalyticsQueryDto query, string fileTitle)
    {
        q = q.Where(f => f.Title == fileTitle);

        if (query.UserId.HasValue)
            q = q.Where(f => f.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(f => f.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(f => f.CreatedAt <= query.ToDate.Value);

        return q;
    }

    /// <summary>Job comparison service page filters by CreatedAt (legacy behavior).</summary>
    public static IQueryable<JobComparison> ApplyByCreatedAt(IQueryable<JobComparison> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(j => j.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(j => j.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(j => j.CreatedAt <= query.ToDate.Value);
        return q;
    }

    /// <summary>Gamification service page filters by StartedAt (legacy behavior).</summary>
    public static IQueryable<GameSession> ApplyByStartedAt(IQueryable<GameSession> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(s => s.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(s => s.StartedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(s => s.StartedAt <= query.ToDate.Value);
        return q;
    }

    public static IQueryable<ChatMessage> ApplyChat(IQueryable<ChatMessage> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(m => m.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(m => m.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(m => m.CreatedAt <= query.ToDate.Value);
        return q;
    }

    public static IQueryable<JobMatchingSearch> Apply(IQueryable<JobMatchingSearch> q, AnalyticsQueryDto query)
    {
        if (query.UserId.HasValue)
            q = q.Where(s => s.UserId == query.UserId.Value);
        if (query.FromDate.HasValue)
            q = q.Where(s => s.SearchedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(s => s.SearchedAt <= query.ToDate.Value);
        return q;
    }
}
