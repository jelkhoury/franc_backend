using FrancProject.Data;
using FrancProject.Dto.AdminAnalytics;
using FrancProject.Helpers;
using FrancProject.Models;
using FrancProject.Models.Analytics;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services.AdminAnalytics;

internal sealed class ServiceAggregateRow
{
    public string ServiceKey { get; init; } = null!;
    public int UserId { get; init; }
    public bool IsCompleted { get; init; }
}

internal static class AnalyticsOverviewAggregator
{
    public static async Task<List<ServiceAggregateRow>> LoadAsync(
        DataContext context,
        AnalyticsQueryDto query,
        bool useActivityEvents)
    {
        if (useActivityEvents && await context.ActivityEvents.AsNoTracking().AnyAsync())
            return await LoadFromActivityEventsAsync(context, query);

        var rows = new List<ServiceAggregateRow>();
        rows.AddRange(await LoadSdsAsync(context, query));
        rows.AddRange(await LoadMockInterviewAsync(context, query));
        rows.AddRange(await LoadJobComparisonAsync(context, query));
        rows.AddRange(await LoadGamificationAsync(context, query));
        rows.AddRange(await LoadFileAsync(context, query, AnalyticsConstants.Resume));
        rows.AddRange(await LoadFileAsync(context, query, AnalyticsConstants.CoverLetter));
        rows.AddRange(await LoadChatAsync(context, query));
        return rows;
    }

    private static async Task<List<ServiceAggregateRow>> LoadFromActivityEventsAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var q = context.ActivityEvents.AsNoTracking().AsQueryable();

        if (query.FromDate.HasValue)
            q = q.Where(e => e.OccurredAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(e => e.OccurredAt <= query.ToDate.Value);
        if (query.UserId.HasValue)
            q = q.Where(e => e.UserId == query.UserId.Value);
        if (!string.IsNullOrWhiteSpace(query.ServiceKey))
            q = q.Where(e => e.ServiceKey == query.ServiceKey);

        var rows = await q
            .Select(e => new { e.ServiceKey, e.UserId, e.Status })
            .ToListAsync();

        return rows.Select(e => new ServiceAggregateRow
        {
            ServiceKey = e.ServiceKey,
            UserId = e.UserId,
            IsCompleted = e.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || e.Status.Equals("evaluated", StringComparison.OrdinalIgnoreCase)
                || e.Status.Equals("uploaded", StringComparison.OrdinalIgnoreCase)
        }).ToList();
    }

    private static async Task<List<ServiceAggregateRow>> LoadSdsAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var rows = await AnalyticsQuerySqlFilters
            .Apply(context.SDSResults.AsNoTracking(), query)
            .Select(r => new { r.UserId, r.IsCompleted })
            .ToListAsync();

        return rows.Select(r => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.Sds,
            UserId = r.UserId,
            IsCompleted = r.IsCompleted
        }).ToList();
    }

    private static async Task<List<ServiceAggregateRow>> LoadMockInterviewAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var answerRows = await AnalyticsQuerySqlFilters
            .ApplyMockInterviewAnswers(context.Answers.AsNoTracking(), query)
            .Select(a => new { a.UserId, a.MockInterview!.IsEvaluated })
            .ToListAsync();

        var reportRows = await AnalyticsQuerySqlFilters
            .ApplyMockInterviewReports(context.EvaluationReports.AsNoTracking(), query)
            .Select(r => new { r.UserId })
            .ToListAsync();

        var rows = new List<ServiceAggregateRow>(answerRows.Count + reportRows.Count);
        rows.AddRange(answerRows.Select(a => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.MockInterview,
            UserId = a.UserId,
            IsCompleted = a.IsEvaluated
        }));
        rows.AddRange(reportRows.Select(r => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.MockInterview,
            UserId = r.UserId,
            IsCompleted = true
        }));
        return rows;
    }

    private static async Task<List<ServiceAggregateRow>> LoadJobComparisonAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var rows = await AnalyticsQuerySqlFilters
            .Apply(context.JobComparisons.AsNoTracking(), query)
            .Select(j => new { j.UserId, j.IsCompleted })
            .ToListAsync();

        return rows.Select(j => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.JobComparison,
            UserId = j.UserId,
            IsCompleted = j.IsCompleted
        }).ToList();
    }

    private static async Task<List<ServiceAggregateRow>> LoadGamificationAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var rows = await AnalyticsQuerySqlFilters
            .Apply(context.GameSessions.AsNoTracking(), query)
            .Select(s => new { s.UserId, s.Status, s.Passed })
            .ToListAsync();

        return rows.Select(s => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.Gamification,
            UserId = s.UserId,
            IsCompleted = s.Status == GameQuizConstants.StatusCompleted && s.Passed
        }).ToList();
    }

    private static async Task<List<ServiceAggregateRow>> LoadFileAsync(
        DataContext context,
        AnalyticsQueryDto query,
        string serviceKey)
    {
        var fileTitle = serviceKey == AnalyticsConstants.Resume ? "Resume" : "CoverLetter";
        var rows = await AnalyticsQuerySqlFilters
            .Apply(context.Files.AsNoTracking(), query, fileTitle)
            .Select(f => new { f.UserId })
            .ToListAsync();

        return rows.Select(f => new ServiceAggregateRow
        {
            ServiceKey = serviceKey,
            UserId = f.UserId,
            IsCompleted = true
        }).ToList();
    }

    private static async Task<List<ServiceAggregateRow>> LoadChatAsync(
        DataContext context,
        AnalyticsQueryDto query)
    {
        var q = context.ChatMessages.AsNoTracking().AsQueryable();

        if (query.UserId.HasValue)
            q = q.Where(m => m.UserId == query.UserId.Value);

        var grouped = q.GroupBy(m => new { m.SessionId, m.UserId });

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value;
            grouped = grouped.Where(g => g.Max(m => m.CreatedAt) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value;
            grouped = grouped.Where(g => g.Max(m => m.CreatedAt) <= to);
        }

        var rows = await grouped
            .Select(g => g.Key.UserId)
            .ToListAsync();

        return rows.Select(userId => new ServiceAggregateRow
        {
            ServiceKey = AnalyticsConstants.Chat,
            UserId = userId,
            IsCompleted = true
        }).ToList();
    }
}
