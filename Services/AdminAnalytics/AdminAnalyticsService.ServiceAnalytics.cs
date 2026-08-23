using FrancProject.Dto.AdminAnalytics;
using FrancProject.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services.AdminAnalytics;

public partial class AdminAnalyticsService
{
    private async Task<SdsServiceAnalyticsDto> GetSdsAnalyticsAsync(AnalyticsQueryDto query)
    {
        var results = await AnalyticsQuerySqlFilters
            .Apply(_context.SDSResults.AsNoTracking(), query)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                r.HollandCode,
                r.IsCompleted,
                r.AttemptNumber,
                OccurredAt = r.IsCompleted && r.CompletedAt.HasValue ? r.CompletedAt.Value : r.CreatedAt
            })
            .ToListAsync();

        var completed = results.Where(r => r.IsCompleted).ToList();
        var userLookup = await BuildUserLookupAsync(
            completed.OrderByDescending(r => r.OccurredAt).Take(10).Select(r => r.UserId));

        return new SdsServiceAnalyticsDto
        {
            Started = results.Count,
            Completed = completed.Count,
            Drafts = results.Count(r => !r.IsCompleted),
            CompletionRate = results.Count > 0
                ? Math.Round(100.0 * completed.Count / results.Count, 1)
                : 0,
            UniqueUsers = results.Select(r => r.UserId).Distinct().Count(),
            ResultDistribution = completed
                .Where(r => !string.IsNullOrWhiteSpace(r.HollandCode))
                .GroupBy(r => r.HollandCode!)
                .Select(g => new HollandCodeCountDto { HollandCode = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList(),
            AttemptDistribution = results
                .GroupBy(r => r.AttemptNumber)
                .Select(g => new AttemptCountDto { AttemptNumber = g.Key, Count = g.Count() })
                .OrderBy(x => x.AttemptNumber)
                .ToList(),
            MostCommonHollandCode = completed
                .Where(r => !string.IsNullOrWhiteSpace(r.HollandCode))
                .GroupBy(r => r.HollandCode!)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault(),
            RecentCompletions = completed
                .OrderByDescending(r => r.OccurredAt)
                .Take(10)
                .Select(r =>
                {
                    userLookup.TryGetValue(r.UserId, out var user);
                    return new SdsRecentCompletionDto
                    {
                        Id = r.Id,
                        UserName = user.FullName,
                        Email = user.Email,
                        HollandCode = r.HollandCode,
                        AttemptNumber = r.AttemptNumber,
                        CompletedAt = r.OccurredAt
                    };
                })
                .ToList()
        };
    }

    private async Task<MockInterviewServiceAnalyticsDto> GetMockInterviewAnalyticsAsync(AnalyticsQueryDto query)
    {
        var answers = await AnalyticsQuerySqlFilters
            .ApplyMockInterviewAnswers(_context.Answers.AsNoTracking(), query)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.CreatedAt,
                MockInterviewId = a.MockInterviewId!.Value,
                a.EvaluationReportId,
                IsEvaluated = a.MockInterview!.IsEvaluated,
                NbOfTry = a.MockInterview!.NbOfTry ?? 1,
                UserFirst = a.User.FirstName,
                UserLast = a.User.LastName,
                UserEmail = a.User.Email,
                MajorName = a.Question!.Major != null ? a.Question.Major.Name : null
            })
            .ToListAsync();

        var reports = await AnalyticsQuerySqlFilters
            .ApplyMockInterviewReports(_context.EvaluationReports.AsNoTracking(), query)
            .Select(r => new { r.Id, r.OverallRating })
            .ToListAsync();

        var answerIds = answers.Select(a => a.Id).ToList();
        var evalQuestions = answerIds.Count == 0
            ? []
            : await _context.EvaluateQuestions
                .AsNoTracking()
                .Where(e => answerIds.Contains(e.AnswerId))
                .Select(e => new { e.Rating, e.Answer.QuestionId, QuestionTitle = e.Answer.Question.Title })
                .ToListAsync();

        var reportLookup = reports.ToDictionary(r => r.Id);
        var ratings = reports.Where(r => r.OverallRating.HasValue).Select(r => r.OverallRating!.Value).ToList();

        return new MockInterviewServiceAnalyticsDto
        {
            Started = answers.Select(a => a.MockInterviewId).Distinct().Count(),
            Completed = answers.Select(a => a.MockInterviewId).Distinct().Count(),
            Evaluated = answers.Where(a => a.IsEvaluated).Select(a => a.MockInterviewId).Distinct().Count(),
            ReportsGenerated = reports.Count,
            UniqueUsers = answers.Select(a => a.UserId).Distinct().Count(),
            AverageOverallRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
            EvaluationDistribution = Enumerable.Range(1, 5)
                .Select(i => new LabelCountDto
                {
                    Label = $"{i} - {(i == 1 ? "Needs improvement" : i == 5 ? "Excellent" : "Rating " + i)}",
                    Count = evalQuestions.Count(e => e.Rating == i)
                })
                .ToList(),
            AttemptDistribution = answers
                .GroupBy(a => a.NbOfTry)
                .Select(g => new LabelCountDto
                {
                    Label = $"Attempt {g.Key}",
                    Count = g.Select(x => x.MockInterviewId).Distinct().Count()
                })
                .OrderBy(x => x.Label)
                .ToList(),
            QuestionPerformance = evalQuestions
                .GroupBy(e => new { e.QuestionId, e.QuestionTitle })
                .Select(g => new QuestionPerformanceDto
                {
                    QuestionId = g.Key.QuestionId,
                    QuestionTitle = g.Key.QuestionTitle,
                    AverageRating = Math.Round(g.Average(x => x.Rating), 1),
                    AnswerCount = g.Count()
                })
                .OrderByDescending(q => q.AnswerCount)
                .Take(20)
                .ToList(),
            RecentInterviews = answers
                .GroupBy(a => a.MockInterviewId)
                .Select(g =>
                {
                    var first = g.OrderByDescending(x => x.CreatedAt).First();
                    float? overallRating = null;
                    var reportGenerated = false;
                    foreach (var answer in g)
                    {
                        if (answer.EvaluationReportId.HasValue &&
                            reportLookup.TryGetValue(answer.EvaluationReportId.Value, out var rep))
                        {
                            reportGenerated = true;
                            overallRating = rep.OverallRating;
                            break;
                        }
                    }

                    return new MockInterviewRecentDto
                    {
                        Id = first.MockInterviewId,
                        UserName = $"{first.UserFirst} {first.UserLast}",
                        Email = first.UserEmail,
                        Major = first.MajorName,
                        Status = first.IsEvaluated ? "evaluated" : "submitted",
                        OverallRating = overallRating,
                        ReportGenerated = reportGenerated,
                        SubmittedAt = g.Max(x => x.CreatedAt)
                    };
                })
                .OrderByDescending(x => x.SubmittedAt)
                .Take(10)
                .ToList()
        };
    }

    private async Task<JobComparisonServiceAnalyticsDto> GetJobComparisonAnalyticsAsync(AnalyticsQueryDto query)
    {
        var comparisons = await AnalyticsQuerySqlFilters
            .ApplyByCreatedAt(_context.JobComparisons.AsNoTracking(), query)
            .Select(j => new
            {
                j.Id,
                j.UserId,
                j.IsCompleted,
                j.CreatedAt,
                j.JobAName,
                j.JobBName,
                UserFirst = j.User.FirstName,
                UserLast = j.User.LastName,
                UserEmail = j.User.Email
            })
            .ToListAsync();

        if (comparisons.Count == 0)
        {
            return new JobComparisonServiceAnalyticsDto
            {
                MostComparedJobs = [],
                TopJobPairs = [],
                WinnerDistribution = [],
                HeadVsHeart = [],
                RecentComparisons = []
            };
        }

        var criteria = await _context.JobComparisonCriteria.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Category);

        var completedIds = comparisons.Where(j => j.IsCompleted).Select(j => j.Id).ToList();
        var answersByComparison = new Dictionary<int, List<JobComparisonScoreHelper.AnswerRow>>();

        if (completedIds.Count > 0)
        {
            var answerRows = await _context.JobComparisonAnswers
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

            foreach (var group in answerRows.GroupBy(a => a.JobComparisonId))
            {
                answersByComparison[group.Key] = group
                    .Select(a => new JobComparisonScoreHelper.AnswerRow(
                        a.ScoreA, a.ScoreB, a.Weight, a.NotApplicableA, a.NotApplicableB,
                        criteria.GetValueOrDefault(a.CriterionId, "HEAD")))
                    .ToList();
            }
        }

        var computed = comparisons.Select(j =>
        {
            answersByComparison.TryGetValue(j.Id, out var rows);
            rows ??= [];
            var (scoreA, scoreB, winner) = rows.Count > 0
                ? JobComparisonScoreHelper.ComputeComparisonResult(rows)
                : (0d, 0d, "Tie");

            return new ComparisonComputed
            {
                Id = j.Id,
                UserName = $"{j.UserFirst} {j.UserLast}",
                Email = j.UserEmail,
                JobAName = j.JobAName,
                JobBName = j.JobBName,
                ScoreA = scoreA,
                ScoreB = scoreB,
                Winner = winner,
                IsCompleted = j.IsCompleted,
                CreatedAt = j.CreatedAt,
                CategoryWinners = rows
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
                    .ToList()
            };
        }).ToList();

        var completed = computed.Where(c => c.IsCompleted).ToList();

        return new JobComparisonServiceAnalyticsDto
        {
            TotalComparisons = comparisons.Count,
            CompletedComparisons = completed.Count,
            DraftComparisons = comparisons.Count - completed.Count,
            UniqueUsers = comparisons.Select(j => j.UserId).Distinct().Count(),
            CompletionRate = comparisons.Count > 0
                ? Math.Round(100.0 * completed.Count / comparisons.Count, 1)
                : 0,
            MostComparedJobs = comparisons
                .SelectMany(j => new[] { j.JobAName, j.JobBName })
                .GroupBy(n => n)
                .Select(g => new JobNameCountDto { JobName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList(),
            TopJobPairs = comparisons
                .GroupBy(j => new
                {
                    A = string.CompareOrdinal(j.JobAName, j.JobBName) <= 0 ? j.JobAName : j.JobBName,
                    B = string.CompareOrdinal(j.JobAName, j.JobBName) <= 0 ? j.JobBName : j.JobAName
                })
                .Select(g => new JobPairCountDto { JobA = g.Key.A, JobB = g.Key.B, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList(),
            WinnerDistribution = completed
                .GroupBy(c => c.Winner)
                .Select(g => new WinnerCountDto { Winner = g.Key, Count = g.Count() })
                .ToList(),
            HeadVsHeart = new[] { "HEAD", "HEART" }.Select(category =>
            {
                var catResults = completed
                    .SelectMany(c => c.CategoryWinners.Where(w => w.Category == category))
                    .ToList();
                return new HeadVsHeartCategoryDto
                {
                    Category = category,
                    JobAWins = catResults.Count(w => w.Winner == "A"),
                    JobBWins = catResults.Count(w => w.Winner == "B"),
                    Ties = catResults.Count(w => w.Winner == "Tie")
                };
            }).ToList(),
            RecentComparisons = computed
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .Select(c => new JobComparisonRecentDto
                {
                    Id = c.Id,
                    UserName = c.UserName,
                    Email = c.Email,
                    JobAName = c.JobAName,
                    JobBName = c.JobBName,
                    ScoreA = Math.Round(c.ScoreA, 1),
                    ScoreB = Math.Round(c.ScoreB, 1),
                    Winner = c.Winner,
                    Status = c.IsCompleted ? "completed" : "draft",
                    CreatedAt = c.CreatedAt
                })
                .ToList()
        };
    }

    private async Task<GamificationServiceAnalyticsDto> GetGamificationAnalyticsAsync(AnalyticsQueryDto query)
    {
        var sessions = await AnalyticsQuerySqlFilters
            .ApplyByStartedAt(_context.GameSessions.AsNoTracking(), query)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.LevelId,
                s.Score,
                s.Passed,
                s.Status,
                LevelNumber = s.Level.LevelNumber,
                LevelName = s.Level.Name
            })
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var answers = sessionIds.Count == 0
            ? []
            : await _context.GameSessionAnswers
                .AsNoTracking()
                .Where(a => sessionIds.Contains(a.SessionId))
                .Select(a => new
                {
                    a.IsCorrect,
                    a.AnsweredAt,
                    a.UsedSkip,
                    a.UsedFiftyFifty,
                    a.UsedDoubleChance,
                    a.UsedTimeFreeze
                })
                .ToListAsync();

        var levels = await _context.GameLevels.AsNoTracking().OrderBy(l => l.LevelNumber).ToListAsync();
        var correct = answers.Count(a => a.IsCorrect);

        var questionStats = sessionIds.Count == 0
            ? new List<(long QuestionId, string QuestionText, int TimesAnswered, int Incorrect)>()
            : await _context.GameSessionAnswers
                .AsNoTracking()
                .Where(a => sessionIds.Contains(a.SessionId))
                .GroupBy(a => new { a.QuestionId, a.Question.QuestionText })
                .Select(g => new ValueTuple<long, string, int, int>(
                    g.Key.QuestionId,
                    g.Key.QuestionText,
                    g.Count(),
                    g.Count(x => !x.IsCorrect)))
                .ToListAsync();

        return new GamificationServiceAnalyticsDto
        {
            TotalPlayers = sessions.Select(s => s.UserId).Distinct().Count(),
            GameSessions = sessions.Count,
            QuestionsAnswered = answers.Count(a => a.AnsweredAt.HasValue),
            CorrectAnswers = correct,
            IncorrectAnswers = answers.Count(a => !a.IsCorrect && a.AnsweredAt.HasValue),
            AccuracyRate = answers.Count > 0
                ? Math.Round(100.0 * correct / answers.Count, 1)
                : 0,
            AverageScore = sessions.Count > 0
                ? Math.Round(sessions.Average(s => s.Score), 1)
                : 0,
            TimeoutCount = sessions.Count(s => s.Status == GameQuizConstants.StatusFailed),
            LevelProgression = levels.Select(level =>
            {
                var levelSessions = sessions.Where(s => s.LevelId == level.Id).ToList();
                var started = levelSessions.Count;
                var completed = levelSessions.Count(s => s.Passed);
                return new LevelProgressionDto
                {
                    LevelNumber = level.LevelNumber,
                    LevelName = level.Name,
                    Started = started,
                    Completed = completed,
                    DropOffRate = started > 0
                        ? Math.Round(100.0 * (started - completed) / started, 1)
                        : 0
                };
            }).ToList(),
            AbilityUsage =
            [
                new AbilityUsageDto { Ability = "Skip", UsageCount = answers.Count(a => a.UsedSkip) },
                new AbilityUsageDto { Ability = "FiftyFifty", UsageCount = answers.Count(a => a.UsedFiftyFifty) },
                new AbilityUsageDto { Ability = "DoubleChance", UsageCount = answers.Count(a => a.UsedDoubleChance) },
                new AbilityUsageDto { Ability = "TimeFreeze", UsageCount = answers.Count(a => a.UsedTimeFreeze) }
            ],
            HardestQuestions = ToQuestionDifficulty(questionStats, byCorrectRate: false),
            EasiestQuestions = ToQuestionDifficulty(questionStats, byCorrectRate: true)
        };
    }

    private static List<QuestionDifficultyDto> ToQuestionDifficulty(
        List<(long QuestionId, string QuestionText, int TimesAnswered, int Incorrect)> questionStats,
        bool byCorrectRate)
    {
        return questionStats
            .Where(q => q.TimesAnswered >= 3)
            .Select(q => new QuestionDifficultyDto
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                TimesAnswered = q.TimesAnswered,
                IncorrectRate = Math.Round(100.0 * q.Incorrect / q.TimesAnswered, 1),
                CorrectRate = Math.Round(100.0 * (q.TimesAnswered - q.Incorrect) / q.TimesAnswered, 1)
            })
            .OrderByDescending(q => byCorrectRate ? q.CorrectRate : q.IncorrectRate)
            .Take(10)
            .ToList();
    }

    private async Task<FileServiceAnalyticsDto> GetFileAnalyticsAsync(AnalyticsQueryDto query, string title)
    {
        var files = await AnalyticsQuerySqlFilters
            .Apply(_context.Files.AsNoTracking(), query, title)
            .Select(f => new
            {
                f.Id,
                f.UserId,
                f.CreatedAt,
                f.ResumeUrl,
                f.CoverUrl,
                UserFirst = f.User.FirstName,
                UserLast = f.User.LastName,
                UserEmail = f.User.Email
            })
            .ToListAsync();

        return new FileServiceAnalyticsDto
        {
            Uploads = files.Count,
            UniqueUsers = files.Select(f => f.UserId).Distinct().Count(),
            RecentUploads = files
                .OrderByDescending(f => f.CreatedAt)
                .Take(10)
                .Select(f => new FileUploadRecentDto
                {
                    Id = f.Id,
                    UserName = $"{f.UserFirst} {f.UserLast}",
                    Email = f.UserEmail,
                    FileName = title == "Resume"
                        ? (Path.GetFileName(f.ResumeUrl ?? "resume.pdf") ?? "resume.pdf")
                        : (Path.GetFileName(f.CoverUrl ?? "cover-letter.pdf") ?? "cover-letter.pdf"),
                    UploadedAt = f.CreatedAt
                })
                .ToList()
        };
    }

    private async Task<ChatServiceAnalyticsDto> GetChatAnalyticsAsync(AnalyticsQueryDto query)
    {
        var sessionRows = await AnalyticsQuerySqlFilters
            .ApplyChat(_context.ChatMessages.AsNoTracking(), query)
            .GroupBy(m => new { m.SessionId, m.UserId })
            .Select(g => new
            {
                g.Key.SessionId,
                g.Key.UserId,
                MessageCount = g.Count(),
                LastMessageAt = g.Max(m => m.CreatedAt)
            })
            .ToListAsync();

        var userLookup = await BuildUserLookupAsync(sessionRows.Select(s => s.UserId));
        var sessions = sessionRows.Select(s =>
        {
            userLookup.TryGetValue(s.UserId, out var user);
            return new ChatSessionRecentDto
            {
                Id = s.SessionId,
                UserName = user.FullName,
                Email = user.Email,
                MessageCount = s.MessageCount,
                LastMessageAt = s.LastMessageAt
            };
        }).ToList();

        var totalMessages = sessionRows.Sum(s => s.MessageCount);
        return new ChatServiceAnalyticsDto
        {
            TotalSessions = sessions.Count,
            UniqueUsers = sessions.Select(s => s.Email).Distinct().Count(),
            TotalMessages = totalMessages,
            AverageMessagesPerSession = sessions.Count > 0
                ? Math.Round((double)totalMessages / sessions.Count, 1)
                : 0,
            RecentSessions = sessions.OrderByDescending(s => s.LastMessageAt).Take(10).ToList()
        };
    }

    private async Task<JobMatchingServiceAnalyticsDto> GetJobMatchingAnalyticsAsync(AnalyticsQueryDto query)
    {
        var searches = await AnalyticsQuerySqlFilters
            .Apply(_context.JobMatchingSearches.AsNoTracking(), query)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.Major,
                s.Country,
                s.ResultsCount,
                s.SearchedAt,
                UserFirst = s.User.FirstName,
                UserLast = s.User.LastName,
                UserEmail = s.User.Email
            })
            .ToListAsync();

        if (searches.Count == 0)
        {
            return new JobMatchingServiceAnalyticsDto
            {
                TotalSearches = 0,
                UniqueUsers = 0,
                Phase2Note = "No logged searches yet. Job matching history is recorded from Phase 2 deploy forward."
            };
        }

        return new JobMatchingServiceAnalyticsDto
        {
            TotalSearches = searches.Count,
            UniqueUsers = searches.Select(s => s.UserId).Distinct().Count(),
            TopMajors = searches
                .Where(s => !string.IsNullOrWhiteSpace(s.Major))
                .GroupBy(s => s.Major!)
                .Select(g => new MajorCountDto { Major = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList(),
            TopCountries = searches
                .Where(s => !string.IsNullOrWhiteSpace(s.Country))
                .GroupBy(s => s.Country!)
                .Select(g => new CountryCountDto { Country = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList(),
            RecentSearches = searches
                .OrderByDescending(s => s.SearchedAt)
                .Take(10)
                .Select(s => new JobMatchingRecentSearchDto
                {
                    Id = s.Id,
                    UserName = $"{s.UserFirst} {s.UserLast}",
                    Email = s.UserEmail,
                    Major = s.Major,
                    Country = s.Country,
                    ResultsCount = s.ResultsCount,
                    SearchedAt = s.SearchedAt
                })
                .ToList()
        };
    }
}
