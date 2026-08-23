using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Helpers;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services;

public class EvaluationService : IEvaluationService
{
    private readonly DataContext _context;
    private readonly IActivityEventWriter _activityEvents;

    public EvaluationService(DataContext context, IActivityEventWriter activityEvents)
    {
        _context = context;
        _activityEvents = activityEvents;
    }

    // ---------------------------------------
    // SINGLE QUESTION EVALUATION
    // ---------------------------------------
    public async Task EvaluateQuestionAsync(int answerId, int evaluatorId, int rating, string? comment, string? tips = null)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        var existing = await _context.EvaluateQuestions
            .FirstOrDefaultAsync(e => e.AnswerId == answerId && e.EvaluatorId == evaluatorId);

        if (existing != null)
        {
            existing.Rating = rating;
            existing.Comment = comment;
            existing.Tips = tips;
            existing.CreatedAt = DateTime.UtcNow;
            _context.EvaluateQuestions.Update(existing);
        }
        else
        {
            var eval = new EvaluateQuestion
            {
                AnswerId = answerId,
                EvaluatorId = evaluatorId,
                Rating = rating,
                Comment = comment,
                Tips = tips,
                CreatedAt = DateTime.UtcNow
            };

            await _context.EvaluateQuestions.AddAsync(eval);
        }

        await _context.SaveChangesAsync();
    }

    // ---------------------------------------
    // FULL REPORT WITH EVALUATIONS
    // ---------------------------------------
    public async Task<EvaluationReportDto> CreateEvaluationReportWithEvaluationsAsync(
        int userId,
        List<int> answerIds,
        string? summaryComment = null,
        List<ReportSkillScoreInputDto>? skillScores = null)
    {
        var report = new EvaluationReport
        {
            UserId = userId,
            GeneratedAt = DateTime.UtcNow,
            SummaryComment = summaryComment
        };

        _context.EvaluationReports.Add(report);
        await _context.SaveChangesAsync();

        var answers = await _context.Answers
            .Include(a => a.Question)
            .Where(a => answerIds.Contains(a.Id))
            .ToListAsync();

        foreach (var a in answers)
        {
            a.EvaluationReportId = report.Id;
        }

        var mockInterviewIds = answers
            .Where(a => a.MockInterviewId != null)
            .Select(a => a.MockInterviewId!.Value)
            .Distinct()
            .ToList();

        if (mockInterviewIds.Count > 0)
        {
            var mockInterviews = await _context.MockInterviews
                .Where(m => mockInterviewIds.Contains(m.Id))
                .ToListAsync();

            foreach (var mock in mockInterviews)
                mock.IsEvaluated = true;
        }

        await _context.SaveChangesAsync();

        var evaluations = await _context.EvaluateQuestions
            .Where(eq => answerIds.Contains(eq.AnswerId))
            .ToListAsync();

        if (evaluations.Any())
        {
            report.OverallRating = (float?)evaluations.Average(e => e.Rating) ?? 0;
            _context.EvaluationReports.Update(report);
            await _context.SaveChangesAsync();

            await _activityEvents.TryLogAsync(
                userId,
                AnalyticsConstants.MockInterview,
                ActivityEventTypes.MockInterviewEvaluated,
                "evaluated",
                report.Id.ToString(),
                report.GeneratedAt,
                report.OverallRating.HasValue ? $"{report.OverallRating:0.#}/5" : null);

            await _activityEvents.TryLogAsync(
                userId,
                AnalyticsConstants.MockInterview,
                ActivityEventTypes.MockInterviewReportGenerated,
                "evaluated",
                $"report-{report.Id}",
                report.GeneratedAt,
                report.OverallRating.HasValue ? $"{report.OverallRating:0.#}/5" : null);
        }

        if (skillScores != null && skillScores.Count > 0)
        {
            foreach (var skill in skillScores)
            {
                if (string.IsNullOrWhiteSpace(skill.SkillCode))
                    throw new ArgumentException("SkillCode is required for each skill score.");
                if (string.IsNullOrWhiteSpace(skill.SkillName))
                    throw new ArgumentException("SkillName is required for each skill score.");
                if (skill.Rating < 1 || skill.Rating > 5)
                    throw new ArgumentOutOfRangeException(nameof(skill.Rating), "Skill rating must be between 1 and 5.");

                await _context.EvaluationReportSkills.AddAsync(new EvaluationReportSkill
                {
                    EvaluationReportId = report.Id,
                    SkillCode = skill.SkillCode.Trim(),
                    SkillName = skill.SkillName.Trim(),
                    Rating = skill.Rating
                });
            }

            await _context.SaveChangesAsync();
        }

        // Allow user to do mock interview
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.CanDoMockInterview = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        var answerDtos = answers.Select(a =>
        {
            var eval = evaluations.FirstOrDefault(e => e.AnswerId == a.Id);
            return new AnswerEvaluationDto
            {
                AnswerId = a.Id,
                VideoUrl = a.VideoUrl,
                QuestionId = a.QuestionId,
                QuestionTitle = a.Question?.Title,
                Comment = eval?.Comment,
                Tips = eval?.Tips,
                Rating = eval?.Rating
            };
        }).ToList();

        var savedSkillScores = await _context.EvaluationReportSkills
            .AsNoTracking()
            .Where(s => s.EvaluationReportId == report.Id)
            .OrderBy(s => s.SkillCode)
            .ToListAsync();

        return new EvaluationReportDto
        {
            Id = report.Id,
            OverallRating = report.OverallRating,
            SummaryComment = report.SummaryComment,
            GeneratedAt = report.GeneratedAt,
            Answers = answerDtos,
            SkillScores = MapSkillScoresToDto(savedSkillScores)
        };
    }

    private static List<ReportSkillScoreDto> MapSkillScoresToDto(List<EvaluationReportSkill> skills)
    {
        return skills.Select(s => new ReportSkillScoreDto
        {
            SkillCode = s.SkillCode,
            SkillName = s.SkillName,
            Rating = s.Rating
        }).ToList();
    }


    // ---------------------------------------
    // INCREASE ATTEMPT
    // ---------------------------------------
    public async Task IncreaseMockAttemptsAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        user.MockAttempts = (user.MockAttempts ?? 0) + 1;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    // ---------------------------------------
    // BULK EVALUATION
    // ---------------------------------------
    public async Task EvaluateAnswersAsync(int evaluatorId, List<EvaluateAnswerDto> evaluations)
    {
        foreach (var eval in evaluations)
        {
            if (eval.Rating < 1 || eval.Rating > 5)
                throw new ArgumentOutOfRangeException(nameof(eval.Rating), "Rating must be between 1 and 5.");

            var existing = await _context.EvaluateQuestions
                .FirstOrDefaultAsync(e => e.AnswerId == eval.AnswerId && e.EvaluatorId == evaluatorId);

            if (existing != null)
            {
                existing.Rating = eval.Rating;
                existing.Comment = eval.Comment;
                existing.Tips = eval.Tips;
                existing.CreatedAt = DateTime.UtcNow;
                _context.EvaluateQuestions.Update(existing);
            }
            else
            {
                var newEval = new EvaluateQuestion
                {
                    AnswerId = eval.AnswerId,
                    EvaluatorId = evaluatorId,
                    Rating = eval.Rating,
                    Comment = eval.Comment,
                    Tips = eval.Tips,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.EvaluateQuestions.AddAsync(newEval);
            }
        }

        await _context.SaveChangesAsync();
    }


    public async Task<List<EvaluationReportDto>> GetReportsByUserIdAsync(int userId)
    {
        var reports = await _context.EvaluationReports
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .OrderByDescending(r => r.GeneratedAt)
            .ToListAsync();

        var answerIds = reports
            .SelectMany(r => r.Answers)
            .Select(a => a.Id)
            .ToList();

        var evaluations = await _context.EvaluateQuestions
            .Where(e => answerIds.Contains(e.AnswerId))
            .ToListAsync();

        var reportIds = reports.Select(r => r.Id).ToList();

        var allSkillScores = await _context.EvaluationReportSkills
            .AsNoTracking()
            .Where(s => reportIds.Contains(s.EvaluationReportId))
            .OrderBy(s => s.SkillCode)
            .ToListAsync();

        var skillsByReport = allSkillScores
            .GroupBy(s => s.EvaluationReportId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = reports.Select(r => new EvaluationReportDto
        {
            Id = r.Id,
            OverallRating = r.OverallRating,
            SummaryComment = r.SummaryComment,
            GeneratedAt = r.GeneratedAt,
            SkillScores = skillsByReport.TryGetValue(r.Id, out var skills)
                ? MapSkillScoresToDto(skills)
                : new List<ReportSkillScoreDto>(),

            Answers = r.Answers.Select(a =>
            {
                var eval = evaluations.FirstOrDefault(e => e.AnswerId == a.Id);

                return new AnswerEvaluationDto
                {
                    AnswerId = a.Id,
                    VideoUrl = a.VideoUrl,
                    QuestionId = a.QuestionId,
                    QuestionTitle = a.Question?.Title,
                    Comment = eval?.Comment,
                    Tips = eval?.Tips,
                    Rating = eval?.Rating
                };
            }).ToList()
        }).ToList();

        return result;
    }

    public async Task<MockInterviewEvaluationsDto?> GetEvaluationsByMockInterviewIdAsync(int mockInterviewId)
    {
        var mock = await _context.MockInterviews
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mockInterviewId);

        if (mock == null)
            return null;

        var answers = await _context.Answers
            .AsNoTracking()
            .Include(a => a.Question)
            .Where(a => a.MockInterviewId == mockInterviewId)
            .OrderBy(a => a.Id)
            .ToListAsync();

        var answerIds = answers.Select(a => a.Id).ToList();

        var evaluateQuestions = await _context.EvaluateQuestions
            .AsNoTracking()
            .Where(e => answerIds.Contains(e.AnswerId))
            .ToListAsync();

        var evaluationsByAnswer = evaluateQuestions
            .GroupBy(e => e.AnswerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

        var userId = answers.FirstOrDefault()?.UserId ?? 0;

        var reportId = answers.FirstOrDefault(a => a.EvaluationReportId != null)?.EvaluationReportId;
        var skillScores = new List<ReportSkillScoreDto>();

        if (reportId != null)
        {
            var reportSkills = await _context.EvaluationReportSkills
                .AsNoTracking()
                .Where(s => s.EvaluationReportId == reportId)
                .OrderBy(s => s.SkillCode)
                .ToListAsync();

            skillScores = MapSkillScoresToDto(reportSkills);
        }

        return new MockInterviewEvaluationsDto
        {
            MockInterviewId = mock.Id,
            MockInterviewTitle = mock.Title,
            IsEvaluated = mock.IsEvaluated,
            UserId = userId,
            SkillScores = skillScores,
            Evaluations = answers.Select(a =>
            {
                evaluationsByAnswer.TryGetValue(a.Id, out var eval);
                return new MockInterviewAnswerEvaluationDto
                {
                    AnswerId = a.Id,
                    QuestionId = a.QuestionId,
                    QuestionTitle = a.Question?.Title ?? string.Empty,
                    VideoUrl = a.VideoUrl,
                    EvaluateQuestionId = eval?.Id,
                    EvaluatorId = eval?.EvaluatorId,
                    Rating = eval?.Rating,
                    Comment = eval?.Comment,
                    Tips = eval?.Tips
                };
            }).ToList()
        };
    }
}
