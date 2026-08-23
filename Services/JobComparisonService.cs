using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using FrancProject.Helpers;
using FrancProject.Interfaces;

namespace FrancProject.Services;

public class JobComparisonService : IJobComparisonService
{
    private readonly DataContext _context;
    private readonly IActivityEventWriter _activityEvents;

    public JobComparisonService(DataContext context, IActivityEventWriter activityEvents)
    {
        _context = context;
        _activityEvents = activityEvents;
    }

    // ===== CRITERIA =====

    public async Task<List<JobComparisonCriterion>> GetCriteriaAsync()
    {
        return await _context.JobComparisonCriteria
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<JobComparisonCriterion> CreateCriterionAsync(JobComparisonCriterion criterion)
    {
        _context.JobComparisonCriteria.Add(criterion);
        await _context.SaveChangesAsync();
        return criterion;
    }

    public async Task<List<JobComparisonCriterion>> BulkCreateCriteriaAsync(
    List<CreateJobComparisonCriterionDto> dtos)
    {
        if (dtos == null || !dtos.Any())
            throw new ArgumentException("Criteria list is empty");

        var entities = dtos.Select(d => new JobComparisonCriterion
        {
            Name = d.Name.Trim(),
            Section = d.Section.Trim(),
            Category = d.Category.Trim().ToUpper(), // enforce HEAD / HEART
            Description = d.Description?.Trim(),
            DisplayOrder = d.DisplayOrder,
            IsActive = d.IsActive,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _context.JobComparisonCriteria.AddRangeAsync(entities);
        await _context.SaveChangesAsync();

        return entities;
    }


    public async Task<JobComparisonCriterion?> UpdateCriterionAsync(
        int id,
        JobComparisonCriterion updated)
    {
        var existing = await _context.JobComparisonCriteria.FindAsync(id);
        if (existing == null) return null;

        existing.Name = updated.Name;
        existing.Section = updated.Section;
        existing.Category = updated.Category;
        existing.Description = updated.Description;
        existing.DisplayOrder = updated.DisplayOrder;
        existing.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteCriterionAsync(int id)
    {
        var criterion = await _context.JobComparisonCriteria.FindAsync(id);
        if (criterion == null) return false;

        _context.JobComparisonCriteria.Remove(criterion);
        await _context.SaveChangesAsync();
        return true;
    }

    // ===== SAVE JOB COMPARISON (CREATE / UPDATE) =====
    public async Task<int> SaveJobComparisonAsync(int userId, SaveJobComparisonDto dto)
    {
        JobComparison comparison;

        if (dto.JobComparisonId == 0)
        {
            comparison = new JobComparison
            {
                UserId = userId,
                JobAName = dto.JobAName,
                JobBName = dto.JobBName,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.JobComparisons.Add(comparison);
            await _context.SaveChangesAsync();

            await _activityEvents.TryLogAsync(
                userId,
                AnalyticsConstants.JobComparison,
                ActivityEventTypes.JobComparisonStarted,
                "draft",
                comparison.Id.ToString(),
                comparison.CreatedAt);
        }
        else
        {
            comparison = await _context.JobComparisons
                .FirstOrDefaultAsync(j =>
                    j.Id == dto.JobComparisonId &&
                    j.UserId == userId);

            if (comparison == null)
                throw new InvalidOperationException("Job comparison not found");

            comparison.UpdatedAt = DateTime.UtcNow;
        }

        // ? No tracking = faster query, shorter connection usage
        var existingAnswers = await _context.JobComparisonAnswers
            .AsNoTracking()
            .Where(x => x.JobComparisonId == comparison.Id)
            .ToDictionaryAsync(x => x.CriterionId);

        var answersToAdd = new List<JobComparisonAnswer>();

        foreach (var a in dto.Answers)
        {
            if (!existingAnswers.TryGetValue(a.CriterionId, out var existing))
            {
                answersToAdd.Add(new JobComparisonAnswer
                {
                    JobComparisonId = comparison.Id,
                    CriterionId = a.CriterionId,
                    NotApplicableA = a.NotApplicableA,
                    NotApplicableB = a.NotApplicableB,
                    Weight = a.Weight,
                    ScoreA = a.NotApplicableA ? 0 : a.ScoreA,
                    ScoreB = a.NotApplicableB ? 0 : a.ScoreB,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Attach only when needed (avoids tracking everything)
                _context.JobComparisonAnswers.Attach(existing);

                existing.NotApplicableA = a.NotApplicableA;
                existing.NotApplicableB = a.NotApplicableB;
                existing.Weight = a.Weight;
                existing.ScoreA = a.NotApplicableA ? 0 : a.ScoreA;
                existing.ScoreB = a.NotApplicableB ? 0 : a.ScoreB;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (answersToAdd.Count > 0)
            _context.JobComparisonAnswers.AddRange(answersToAdd);

        if (dto.IsCompleted)
        {
            comparison.IsCompleted = true;
            comparison.CompletedAt = DateTime.UtcNow;
        }

        // ? SINGLE SaveChanges = shorter connection lifetime
        await _context.SaveChangesAsync();

        if (dto.IsCompleted)
        {
            await _activityEvents.TryLogAsync(
                userId,
                AnalyticsConstants.JobComparison,
                ActivityEventTypes.JobComparisonCompleted,
                "completed",
                comparison.Id.ToString(),
                comparison.CompletedAt ?? DateTime.UtcNow);
        }

        return comparison.Id;
    }






    // ===== READ =====

    public async Task<JobComparison?> GetJobComparisonAsync(int userId, int jobComparisonId)
    {
        return await _context.JobComparisons
            .Include(j => j.Answers)
            .FirstOrDefaultAsync(j =>
                j.Id == jobComparisonId &&
                j.UserId == userId);
    }

    public async Task<List<JobComparison>> GetAllJobComparisonsAsync(int userId)
    {
        return await _context.JobComparisons
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteJobComparisonAsync(int userId, int jobComparisonId)
    {
        var comparison = await _context.JobComparisons
            .FirstOrDefaultAsync(j =>
                j.Id == jobComparisonId &&
                j.UserId == userId);

        if (comparison == null) return false;

        _context.JobComparisons.Remove(comparison);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<JobComparison?> GetLatestIncompleteJobComparisonAsync(int userId)
    {
        var latest = await _context.JobComparisons
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new
            {
                j.Id,
                j.IsCompleted
            })
            .FirstOrDefaultAsync();

        if (latest == null)
            return null;

        if (latest.IsCompleted)
            return null;
        return await GetJobComparisonAsync(userId, latest.Id);
    }


    public async Task<List<JobComparisonDto>> GetAllJobComparisonsByUserId(int userId)
    {
        return await _context.JobComparisons
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobComparisonDto
            {
                Id = j.Id,
                JobAName = j.JobAName,
                JobBName = j.JobBName,
                IsCompleted = j.IsCompleted,
                CreatedAt = j.CreatedAt,
                ExcelResultUrl = j.ExcelResultUrl,
                UserId = j.UserId,
                Answers = j.Answers.Select(a => new JobComparisonAnswerDto
                {
                    CriterionId = a.CriterionId,
                    Weight = a.Weight,
                    ScoreA = a.ScoreA,
                    ScoreB = a.ScoreB,
                    NotApplicableA = a.NotApplicableA,
                    NotApplicableB = a.NotApplicableB
                }).ToList()
            })
            .ToListAsync();
    }

}
