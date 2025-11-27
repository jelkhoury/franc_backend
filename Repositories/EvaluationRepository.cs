using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interface;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;

public class EvaluationRepository : IEvaluationRepository
{
    private readonly DataContext _context;

    public EvaluationRepository(DataContext context)
    {
        _context = context;
    }

    // ---------------------------------------
    // SINGLE QUESTION EVALUATION
    // ---------------------------------------
    public async Task EvaluateQuestionAsync(int answerId, int evaluatorId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        var existing = await _context.EvaluateQuestions
            .FirstOrDefaultAsync(e => e.AnswerId == answerId && e.EvaluatorId == evaluatorId);

        if (existing != null)
        {
            existing.Rating = rating;
            existing.Comment = comment;
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
        string? summaryComment = null)
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

        await _context.SaveChangesAsync();

        var evaluations = await _context.EvaluateQuestions
            .Where(eq => answerIds.Contains(eq.AnswerId))
            .ToListAsync();

        if (evaluations.Any())
        {
            report.OverallRating = (float?)evaluations.Average(e => e.Rating) ?? 0;
            _context.EvaluationReports.Update(report);
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
                Rating = eval?.Rating
            };
        }).ToList();

        return new EvaluationReportDto
        {
            Id = report.Id,
            OverallRating = report.OverallRating,
            SummaryComment = report.SummaryComment,
            GeneratedAt = report.GeneratedAt,
            Answers = answerDtos
        };
    }

    // ---------------------------------------
    // CHECK IF USER CAN DO MOCK
    // ---------------------------------------
    public async Task<bool> CanUserDoMockInterviewAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        if ((bool)!user.CanDoMockInterview)
            return false;

        if ((user.MockAttempts ?? 0) > 1)
            return false;

        int count = await _context.EvaluationReports.CountAsync(r => r.UserId == userId);
        if (count > 1)
            return false;

        return true;
    }

    // ---------------------------------------
    // INCREASE ATTEMPT
    // ---------------------------------------
    public async Task IncreaseMockAttemptsAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

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
                    CreatedAt = DateTime.UtcNow
                };

                await _context.EvaluateQuestions.AddAsync(newEval);
            }
        }

        await _context.SaveChangesAsync();
    }

    
}
