using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrancProject.Data;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using FrancProject.Dto;

public class EvaluationRepository:IEvaluationRepository
{
    private readonly DataContext _context;

    public EvaluationRepository(DataContext context)
    {
        _context = context;
    }


 
    public async Task EvaluateQuestionAsync(int answerId, int evaluatorId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        var existingEvaluation = await _context.EvaluateQuestions
    .FirstOrDefaultAsync(e =>
        e.AnswerId == answerId &&
        e.EvaluatorId == evaluatorId);

        if (existingEvaluation != null)
        {

            existingEvaluation.Rating = rating;
            existingEvaluation.Comment = comment;
            existingEvaluation.CreatedAt = DateTime.UtcNow;
            _context.EvaluateQuestions.Update(existingEvaluation);
        }
        else
        {

            var evaluation = new EvaluateQuestion
            {
                AnswerId = answerId,
                EvaluatorId = evaluatorId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            await _context.EvaluateQuestions.AddAsync(evaluation);
        }

        await _context.SaveChangesAsync();
    }

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

        foreach (var answer in answers)
        {
            answer.EvaluationReportId = report.Id;
        }

        await _context.SaveChangesAsync();

        var evaluations = await _context.EvaluateQuestions
            .Where(eq => answerIds.Contains(eq.AnswerId))
            .ToListAsync();

        float? overallRating = null;
        if (evaluations.Count > 0)
        {
            overallRating = (float)(evaluations.Average(eq => (int?)eq.Rating) ?? 0);
            report.OverallRating = overallRating;
            _context.EvaluationReports.Update(report);
            await _context.SaveChangesAsync();
        }


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




    public async Task<bool> CanUserDoMockInterviewAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        if (user.CanDoMockInterview == false)
            return false;

        if ((user.MockAttempts ?? 0) > 1)
            return false;

        int interviewCount = await _context.EvaluationReports.CountAsync(r => r.UserId == userId);
        if (interviewCount >1)
            return false;

        return true;
    }


    public async Task IncreaseMockAttemptsAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            throw new ArgumentException("User not found");


        user.MockAttempts = (user.MockAttempts ?? 0) + 1;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }




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
