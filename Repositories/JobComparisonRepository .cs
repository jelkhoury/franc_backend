using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using System;

public class JobComparisonRepository : IJobComparisonRepository
{
    private readonly DataContext _context;

    public JobComparisonRepository(DataContext context)
    {
        _context = context;
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

        // 🔥 UPSERT ANSWERS
        foreach (var a in dto.Answers)
        {
            var existing = await _context.JobComparisonAnswers
                .FirstOrDefaultAsync(x =>
                    x.JobComparisonId == comparison.Id &&
                    x.CriterionId == a.CriterionId);

            if (existing == null)
            {
                _context.JobComparisonAnswers.Add(new JobComparisonAnswer
                {
                    JobComparisonId = comparison.Id,
                    CriterionId = a.CriterionId,
                    NotApplicable = a.NotApplicable,
                    Weight = a.NotApplicable ? 0 : a.Weight,
                    ScoreA = a.NotApplicable ? 0 : a.ScoreA,
                    ScoreB = a.NotApplicable ? 0 : a.ScoreB,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.NotApplicable = a.NotApplicable;
                existing.Weight = a.NotApplicable ? 0 : a.Weight;
                existing.ScoreA = a.NotApplicable ? 0 : a.ScoreA;
                existing.ScoreB = a.NotApplicable ? 0 : a.ScoreB;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (dto.IsCompleted)
        {
            comparison.IsCompleted = true;
            comparison.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
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
            .Where(j => j.UserId == userId)
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


}
