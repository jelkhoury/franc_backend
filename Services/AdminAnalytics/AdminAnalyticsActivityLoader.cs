using FrancProject.Data;

using FrancProject.Dto.AdminAnalytics;

using FrancProject.Helpers;

using FrancProject.Models.Analytics;

using Microsoft.EntityFrameworkCore;



namespace FrancProject.Services.AdminAnalytics;



internal sealed class AnalyticsActivityRecord

{

    public string Id { get; init; } = null!;

    public int UserId { get; init; }

    public string ServiceKey { get; init; } = null!;

    public string ActivityLabel { get; init; } = null!;

    public string? Result { get; init; }

    public string Status { get; init; } = null!;

    public DateTime OccurredAt { get; init; }

    public bool IsCompleted { get; init; }

}



internal static class AnalyticsActivityLoader

{

    public static async Task<List<AnalyticsActivityRecord>> LoadAllAsync(

        DataContext context,

        AnalyticsQueryDto query,

        bool useActivityEvents = false)

    {

        if (useActivityEvents)

        {

            var tableHasEvents = await context.ActivityEvents.AsNoTracking().AnyAsync();

            if (tableHasEvents)

            {

                var fromEvents = await LoadFromActivityEventsAsync(context, query);

                return ApplyFilters(fromEvents, query);

            }

        }



        var activities = new List<AnalyticsActivityRecord>();
        activities.AddRange(await LoadSdsAsync(context, query));
        activities.AddRange(await LoadMockInterviewAsync(context, query));
        activities.AddRange(await LoadJobComparisonAsync(context, query));
        activities.AddRange(await LoadGamificationAsync(context, query));
        activities.AddRange(await LoadFileAsync(context, query, AnalyticsConstants.Resume, "Resume"));
        activities.AddRange(await LoadFileAsync(context, query, AnalyticsConstants.CoverLetter, "CoverLetter"));
        activities.AddRange(await LoadChatAsync(context, query));

        return ApplyFilters(activities, query);

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadFromActivityEventsAsync(

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



        var events = await q.ToListAsync();



        return events.Select(e => new AnalyticsActivityRecord

        {

            Id = $"ae-{e.Id}",

            UserId = e.UserId,

            ServiceKey = e.ServiceKey,

            ActivityLabel = MapActivityLabel(e.ActivityType),

            Result = e.ResultSummary,

            Status = e.Status,

            OccurredAt = e.OccurredAt,

            IsCompleted = IsCompletedStatus(e.Status)

        }).ToList();

    }



    private static string MapActivityLabel(string activityType) => activityType switch

    {

        ActivityEventTypes.SdsStarted => "Started SDS",

        ActivityEventTypes.SdsCompleted => "Completed SDS",

        ActivityEventTypes.MockInterviewSubmitted => "Mock Interview Submitted",

        ActivityEventTypes.MockInterviewEvaluated => "Mock Interview Evaluated",

        ActivityEventTypes.MockInterviewReportGenerated => "Mock Interview Report Generated",

        ActivityEventTypes.JobComparisonStarted => "Job Comparison Started",

        ActivityEventTypes.JobComparisonCompleted => "Job Comparison Completed",

        ActivityEventTypes.GamificationLevelCompleted => "Gamification Level Completed",

        ActivityEventTypes.GamificationLevelFailed => "Gamification Level Failed",

        ActivityEventTypes.ResumeUploaded => "Resume Evaluated (uploaded)",

        ActivityEventTypes.CoverLetterUploaded => "Cover Letter Evaluated (uploaded)",

        _ => activityType

    };



    private static bool IsCompletedStatus(string status) =>

        status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||

        status.Equals("evaluated", StringComparison.OrdinalIgnoreCase) ||

        status.Equals("uploaded", StringComparison.OrdinalIgnoreCase);



    private static List<AnalyticsActivityRecord> ApplyFilters(

        List<AnalyticsActivityRecord> activities,

        AnalyticsQueryDto query)

    {

        IEnumerable<AnalyticsActivityRecord> filtered = activities;



        if (query.FromDate.HasValue)

            filtered = filtered.Where(a => a.OccurredAt >= query.FromDate.Value);



        if (query.ToDate.HasValue)

            filtered = filtered.Where(a => a.OccurredAt <= query.ToDate.Value);



        if (query.UserId.HasValue)

            filtered = filtered.Where(a => a.UserId == query.UserId.Value);



        if (!string.IsNullOrWhiteSpace(query.ServiceKey))

            filtered = filtered.Where(a => a.ServiceKey == query.ServiceKey);



        if (!string.IsNullOrWhiteSpace(query.Status))

        {

            var status = query.Status.Trim();

            filtered = filtered.Where(a =>

                a.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        }



        return filtered.ToList();

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadSdsAsync(

        DataContext context,

        AnalyticsQueryDto query)

    {

        var results = await AnalyticsQuerySqlFilters

            .Apply(context.SDSResults.AsNoTracking(), query)

            .Select(r => new

            {

                r.Id,

                r.UserId,

                r.HollandCode,

                r.IsCompleted,

                OccurredAt = r.IsCompleted && r.CompletedAt.HasValue ? r.CompletedAt.Value : r.CreatedAt

            })

            .ToListAsync();



        return results.Select(r => new AnalyticsActivityRecord

        {

            Id = $"sds-{r.Id}",

            UserId = r.UserId,

            ServiceKey = AnalyticsConstants.Sds,

            ActivityLabel = r.IsCompleted ? "Completed SDS" : "Started SDS",

            Result = r.HollandCode,

            Status = r.IsCompleted ? "completed" : "draft",

            OccurredAt = r.OccurredAt,

            IsCompleted = r.IsCompleted

        }).ToList();

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadMockInterviewAsync(

        DataContext context,

        AnalyticsQueryDto query)

    {

        var answers = await AnalyticsQuerySqlFilters

            .ApplyMockInterviewAnswers(context.Answers.AsNoTracking(), query)

            .Select(a => new

            {

                a.Id,

                a.UserId,

                a.CreatedAt,

                a.MockInterview!.IsEvaluated,

                a.EvaluationReportId

            })

            .ToListAsync();



        var reportRatings = await AnalyticsQuerySqlFilters
            .ApplyMockInterviewReports(context.EvaluationReports.AsNoTracking(), query)
            .Select(r => new { r.Id, r.OverallRating, r.GeneratedAt, r.UserId })
            .ToListAsync();

        var reportLookup = reportRatings.ToDictionary(r => r.Id);



        var activities = new List<AnalyticsActivityRecord>();



        foreach (var answer in answers)

        {

            var status = answer.IsEvaluated ? "evaluated"

                : answer.EvaluationReportId.HasValue ? "pending evaluation"

                : "submitted";



            string? result = null;

            if (answer.EvaluationReportId.HasValue &&

                reportLookup.TryGetValue(answer.EvaluationReportId.Value, out var report))

            {

                result = report.OverallRating.HasValue

                    ? $"{report.OverallRating:0.#}/5"

                    : null;

            }



            activities.Add(new AnalyticsActivityRecord

            {

                Id = $"mi-{answer.Id}",

                UserId = answer.UserId,

                ServiceKey = AnalyticsConstants.MockInterview,

                ActivityLabel = answer.IsEvaluated ? "Mock Interview Evaluated" : "Mock Interview Submitted",

                Result = result,

                Status = status,

                OccurredAt = answer.CreatedAt,

                IsCompleted = answer.IsEvaluated

            });

        }



        foreach (var report in reportRatings)

        {

            activities.Add(new AnalyticsActivityRecord

            {

                Id = $"mi-report-{report.Id}",

                UserId = report.UserId,

                ServiceKey = AnalyticsConstants.MockInterview,

                ActivityLabel = "Mock Interview Report Generated",

                Result = report.OverallRating.HasValue ? $"{report.OverallRating:0.#}/5" : null,

                Status = "evaluated",

                OccurredAt = report.GeneratedAt,

                IsCompleted = true

            });

        }



        return activities;

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadJobComparisonAsync(

        DataContext context,

        AnalyticsQueryDto query)

    {

        var comparisons = await AnalyticsQuerySqlFilters

            .Apply(context.JobComparisons.AsNoTracking(), query)

            .Select(j => new

            {

                j.Id,

                j.UserId,

                j.IsCompleted,

                j.CreatedAt,

                j.CompletedAt,

                j.JobAName,

                j.JobBName

            })

            .ToListAsync();



        if (comparisons.Count == 0)

            return [];



        var completedIds = comparisons.Where(c => c.IsCompleted).Select(c => c.Id).ToList();

        Dictionary<int, string> resultByComparisonId = new();



        if (completedIds.Count > 0)

        {

            var answerRows = await context.JobComparisonAnswers

                .AsNoTracking()

                .Where(a => completedIds.Contains(a.JobComparisonId))

                .Select(a => new

                {

                    a.JobComparisonId,

                    a.ScoreA,

                    a.ScoreB,

                    a.Weight,

                    a.NotApplicableA,

                    a.NotApplicableB,

                    a.CriterionId

                })

                .ToListAsync();



            var criteria = await context.JobComparisonCriteria

                .AsNoTracking()

                .ToDictionaryAsync(c => c.Id, c => c.Category);



            foreach (var comparison in comparisons.Where(c => c.IsCompleted))

            {

                var rows = answerRows

                    .Where(a => a.JobComparisonId == comparison.Id)

                    .Select(a => new JobComparisonScoreHelper.AnswerRow(

                        a.ScoreA,

                        a.ScoreB,

                        a.Weight,

                        a.NotApplicableA,

                        a.NotApplicableB,

                        criteria.GetValueOrDefault(a.CriterionId, "HEAD")))

                    .ToList();



                var (_, _, winner) = JobComparisonScoreHelper.ComputeComparisonResult(rows);

                resultByComparisonId[comparison.Id] = JobComparisonScoreHelper.FormatWinnerLabel(

                    winner, comparison.JobAName, comparison.JobBName);

            }

        }



        return comparisons.Select(comparison =>

        {

            var occurredAt = comparison.IsCompleted && comparison.CompletedAt.HasValue

                ? comparison.CompletedAt.Value

                : comparison.CreatedAt;



            return new AnalyticsActivityRecord

            {

                Id = $"jc-{comparison.Id}",

                UserId = comparison.UserId,

                ServiceKey = AnalyticsConstants.JobComparison,

                ActivityLabel = comparison.IsCompleted ? "Job Comparison Completed" : "Job Comparison Started",

                Result = comparison.IsCompleted && resultByComparisonId.TryGetValue(comparison.Id, out var label)

                    ? label

                    : null,

                Status = comparison.IsCompleted ? "completed" : "draft",

                OccurredAt = occurredAt,

                IsCompleted = comparison.IsCompleted

            };

        }).ToList();

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadGamificationAsync(

        DataContext context,

        AnalyticsQueryDto query)

    {

        var sessions = await AnalyticsQuerySqlFilters

            .Apply(context.GameSessions.AsNoTracking(), query)

            .Select(s => new

            {

                s.Id,

                s.UserId,

                s.FinishedAt,

                s.StartedAt,

                s.Passed,

                s.Status,

                s.Score,

                LevelNumber = s.Level.LevelNumber,

                BadgeName = s.Level.BadgeName

            })

            .ToListAsync();



        return sessions.Select(s => new AnalyticsActivityRecord

        {

            Id = $"game-{s.Id}",

            UserId = s.UserId,

            ServiceKey = AnalyticsConstants.Gamification,

            ActivityLabel = s.Passed

                ? $"Completed Level {s.LevelNumber}"

                : s.Status == GameQuizConstants.StatusFailed

                    ? $"Failed Level {s.LevelNumber}"

                    : $"Started Level {s.LevelNumber}",

            Result = s.Passed ? s.BadgeName : $"{s.Score} pts",

            Status = s.Status.Equals(GameQuizConstants.StatusCompleted, StringComparison.OrdinalIgnoreCase)

                ? "completed"

                : s.Status.Equals(GameQuizConstants.StatusFailed, StringComparison.OrdinalIgnoreCase)

                    ? "failed"

                    : "in progress",

            OccurredAt = (s.FinishedAt ?? s.StartedAt).UtcDateTime,

            IsCompleted = s.Status == GameQuizConstants.StatusCompleted && s.Passed

        }).ToList();

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadFileAsync(

        DataContext context,

        AnalyticsQueryDto query,

        string serviceKey,

        string fileTitle)

    {

        var files = await AnalyticsQuerySqlFilters

            .Apply(context.Files.AsNoTracking(), query, fileTitle)

            .Select(f => new

            {

                f.Id,

                f.UserId,

                f.CreatedAt,

                f.AiEvaluation

            })

            .ToListAsync();



        var label = serviceKey == AnalyticsConstants.Resume

            ? "Resume Evaluated"

            : "Cover Letter Evaluated";



        return files.Select(f => new AnalyticsActivityRecord

        {

            Id = $"file-{serviceKey}-{f.Id}",

            UserId = f.UserId,

            ServiceKey = serviceKey,

            ActivityLabel = string.IsNullOrWhiteSpace(f.AiEvaluation) ? $"{label} (uploaded)" : label,

            Result = string.IsNullOrWhiteSpace(f.AiEvaluation) ? "Uploaded" : "AI feedback",

            Status = string.IsNullOrWhiteSpace(f.AiEvaluation) ? "uploaded" : "evaluated",

            OccurredAt = f.CreatedAt,

            IsCompleted = true

        }).ToList();

    }



    private static async Task<List<AnalyticsActivityRecord>> LoadChatAsync(

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



        var sessions = await grouped

            .Select(g => new

            {

                g.Key.SessionId,

                g.Key.UserId,

                MessageCount = g.Count(),

                LastMessageAt = g.Max(m => m.CreatedAt)

            })

            .ToListAsync();



        return sessions.Select(s => new AnalyticsActivityRecord

        {

            Id = $"chat-{s.SessionId}",

            UserId = s.UserId,

            ServiceKey = AnalyticsConstants.Chat,

            ActivityLabel = "Chat Session",

            Result = $"{s.MessageCount} messages",

            Status = "completed",

            OccurredAt = s.LastMessageAt,

            IsCompleted = true

        }).ToList();

    }

}


