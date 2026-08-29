using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Models;
using FrancProject.Helpers;
using FrancProject.Interfaces;

namespace FrancProject.Services;

public class SdsService : ISdsService
{
    private readonly DataContext _context;
    private readonly IConfiguration _configuration;
    private readonly IActivityEventWriter _activityEvents;

    public SdsService(
        DataContext context,
        IConfiguration configuration,
        IActivityEventWriter activityEvents)
    {
        _context = context;
        _configuration = configuration;
        _activityEvents = activityEvents;
    }

    public async Task<int> CreateSectionAsync(CreateSDSSectionDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Section name is required.", nameof(dto));

        var entity = new SDSSection
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim()
        };

        _context.SDSSections.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<IReadOnlyList<SDSSection>> GetAllSectionsWithQuestionsAsync()
    {
        return await _context.SDSSections
            .AsNoTracking()
            .Include(s => s.Questions)
                .ThenInclude(q => q.AnswerOptions)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<int> CreateQuestionWithOptionsAsync(CreateSDSQuestionWithOptionsDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.SectionName))
            throw new ArgumentException("Section name is required.", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Text))
            throw new ArgumentException("Question text is required.", nameof(dto));

        var section = await _context.SDSSections
            .FirstOrDefaultAsync(s => s.Name.ToLower() == dto.SectionName.ToLower());

        if (section == null)
            throw new InvalidOperationException($"Section '{dto.SectionName}' not found.");

        var question = new SDSQuestion
        {
            SectionId = section.Id,
            Text = dto.Text.Trim(),
            Type = dto.Type,
            AnswerOptions = new List<SDSAnswerOption>()
        };

        if (dto.Type != QuestionType.TextBox)
        {
            if (dto.AnswerOptions == null || dto.AnswerOptions.Count == 0)
                throw new InvalidOperationException("Non-TextBox questions must include answer options.");

            foreach (var opt in dto.AnswerOptions)
            {
                if (opt == null) continue;

                var value = string.IsNullOrWhiteSpace(opt.Value)
                    ? null
                    : opt.Value.Trim().ToUpperInvariant();

                question.AnswerOptions.Add(new SDSAnswerOption
                {
                    Text = opt.Text?.Trim(),
                    Value = value
                });
            }
        }

        _context.SDSQuestions.Add(question);
        await _context.SaveChangesAsync();
        return question.Id;
    }

    public async Task<IReadOnlyList<SDSQuestion>> GetQuestionsBySectionAsync(int sectionId)
    {
        return await _context.SDSQuestions
            .AsNoTracking()
            .Where(q => q.SectionId == sectionId)
            .Include(q => q.AnswerOptions)
            .OrderBy(q => q.Id)
            .ToListAsync();
    }

    string? NormalizeSelectedValue(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = v.Trim();
        if (v.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        return v.ToUpperInvariant();
    }
    public async Task<object> SaveUserResponsesAsync(SubmitSDSResponsesDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (dto.UserId <= 0)
            throw new ArgumentException("UserId is required.", nameof(dto));

        if (dto.Responses == null || dto.Responses.Count == 0)
            throw new ArgumentException("At least one response is required.", nameof(dto));

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == dto.UserId);

        if (user == null)
            throw new InvalidOperationException("User not found.");

        if (dto.IsCompleted && user.SDSAttempts <= 0)
        {
            return new
            {
                Success = false,
                Message = "You don't have enough SDS attempts remaining."
            };
        }

        var attempt = await _context.SDSResults
            .Include(r => r.Responses)
            .Where(r => r.UserId == dto.UserId && !r.IsCompleted)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (attempt == null)
        {
            int lastAttemptNumber = await _context.SDSResults
                .Where(r => r.UserId == dto.UserId)
                .MaxAsync(r => (int?)r.AttemptNumber) ?? 0;

            attempt = new SDSResult
            {
                UserId = dto.UserId,
                AttemptNumber = lastAttemptNumber + 1,
                CreatedAt = DateTime.UtcNow,
                IsCompleted = false
            };

            _context.SDSResults.Add(attempt);
            await _context.SaveChangesAsync();

            await _activityEvents.TryLogAsync(
                dto.UserId,
                AnalyticsConstants.Sds,
                ActivityEventTypes.SdsStarted,
                "draft",
                attempt.Id.ToString(),
                attempt.CreatedAt);
        }

        // ?? UPSERT responses
        var existingResponses = (attempt.Responses ?? new List<SDSResponse>())
    .ToDictionary(r => r.QuestionId);
        var now = DateTime.UtcNow;
        foreach (var r in dto.Responses)
        {
            var normalizedSelectedValue = NormalizeSelectedValue(r.SelectedValue);
            var normalizedCustomAnswer = string.IsNullOrWhiteSpace(r.CustomAnswer) ? null : r.CustomAnswer.Trim();

            if (existingResponses.TryGetValue(r.QuestionId, out var existing))
{
    existing.SelectedValue = normalizedSelectedValue;
    existing.CustomAnswer = normalizedCustomAnswer;
    existing.SubmittedAt = now;
    existing.AttemptNumber = attempt.AttemptNumber;
}
else
{
    _context.SDSResponses.Add(new SDSResponse
    {
        SDSResultId = attempt.Id,
        UserId = dto.UserId,
        QuestionId = r.QuestionId,
        SelectedValue = normalizedSelectedValue,
        CustomAnswer = normalizedCustomAnswer,
        SubmittedAt = now,
        AttemptNumber = attempt.AttemptNumber
    });
}
        }

        await _context.SaveChangesAsync();

        // ?? If it's only draft ? return
        if (!dto.IsCompleted)
        {
            return new
            {
                Success = true,
                Attempt = attempt.AttemptNumber,
                SDSResultId = attempt.Id,
                Message = "Draft saved successfully."
            };
        }

        // ?? FINAL SUBMIT LOGIC

        int totalQuestions = await _context.SDSQuestions.CountAsync();
        int answeredCount = await _context.SDSResponses
            .Where(r => r.SDSResultId == attempt.Id)
            .CountAsync();

        if (answeredCount < totalQuestions)
        {
            return new
            {
                Success = false,
                Message = "You must answer all questions before submitting."
            };
        }

        var hollandCode = await CalculateHollandCodeAsync(dto.UserId, attempt.AttemptNumber);

        attempt.HollandCode = hollandCode;
        attempt.IsCompleted = true;
        attempt.CompletedAt = DateTime.UtcNow;

        user.SDSAttempts -= 1;

        await _context.SaveChangesAsync();

        await _activityEvents.TryLogAsync(
            dto.UserId,
            AnalyticsConstants.Sds,
            ActivityEventTypes.SdsCompleted,
            "completed",
            attempt.Id.ToString(),
            attempt.CompletedAt ?? DateTime.UtcNow,
            hollandCode);

        return new
        {
            Success = true,
            Attempt = attempt.AttemptNumber,
            HollandCode = hollandCode,
            SDSResultId = attempt.Id
        };
    }


    public async Task<Dictionary<string, Dictionary<string, int>>> CalculateHollandPointsBySectionNameAsync(int userId)
    {
        var validCodes = new HashSet<string> { "R", "I", "A", "S", "E", "C" };

        int lastAttempt = await _context.SDSResponses
            .Where(r => r.UserId == userId)
            .MaxAsync(r => (int?)r.AttemptNumber) ?? 0;

        if (lastAttempt == 0)
        {
            return new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        }

        var responses = await _context.SDSResponses
            .AsNoTracking()
            .Where(r => r.UserId == userId &&
                        r.AttemptNumber == lastAttempt &&
                        !string.IsNullOrWhiteSpace(r.SelectedValue))
            .Include(r => r.Question)
                .ThenInclude(q => q.Section)
            .ToListAsync();

        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in responses)
        {
            var sectionName = r.Question.Section.Name;

            if (!result.ContainsKey(sectionName))
            {
                result[sectionName] = validCodes.ToDictionary(c => c, c => 0);
            }

            var value = r.SelectedValue.Trim().ToUpper();

            var codeLetter = value[^1].ToString();
            if (!validCodes.Contains(codeLetter))
                continue;

            var numberPart = value.Length > 1 ? value[..^1] : "";
            int score = 1;

            if (!string.IsNullOrWhiteSpace(numberPart) &&
                int.TryParse(numberPart, out int parsed))
            {
                score = parsed;
            }

            result[sectionName][codeLetter] += score;
        }

        return result;
    }

    public async Task<Dictionary<string, Dictionary<string, int>>>
    CalculateHollandPointsBySectionNameForAttemptAsync(int userId, int attemptNumber)
    {
        var validCodes = new HashSet<string> { "R", "I", "A", "S", "E", "C" };

        if (attemptNumber <= 0)
        {
            return new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        }

        var responses = await _context.SDSResponses
            .AsNoTracking()
            .Where(r => r.UserId == userId &&
                        r.AttemptNumber == attemptNumber &&
                        !string.IsNullOrWhiteSpace(r.SelectedValue))
            .Include(r => r.Question)
                .ThenInclude(q => q.Section)
            .ToListAsync();

        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in responses)
        {
            var sectionName = r.Question.Section.Name;

            if (!result.ContainsKey(sectionName))
            {
                result[sectionName] = validCodes.ToDictionary(c => c, c => 0);
            }

            var value = r.SelectedValue.Trim().ToUpper();

            var codeLetter = value[^1].ToString();
            if (!validCodes.Contains(codeLetter))
                continue;

            var numberPart = value.Length > 1 ? value[..^1] : "";
            int score = 1;

            if (!string.IsNullOrWhiteSpace(numberPart) &&
                int.TryParse(numberPart, out int parsed))
            {
                score = parsed;
            }

            result[sectionName][codeLetter] += score;
        }

        return result;
    }


    public async Task<IReadOnlyList<SDSResponse>> GetUserResponsesAsync(int userId)
    {
        return await _context.SDSResponses
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.QuestionId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> CreateQuestionsWithOptionsAsync(
        IEnumerable<CreateSDSQuestionWithOptionsDto> dtos)
    {
        if (dtos == null)
            throw new ArgumentNullException(nameof(dtos));

        var dtoList = dtos.ToList();
        if (dtoList.Count == 0)
            return Array.Empty<int>();

        for (int i = 0; i < dtoList.Count; i++)
        {
            var dto = dtoList[i];

            if (dto == null)
                throw new ArgumentException($"Question DTO is null at index {i}.", nameof(dtos));

            if (string.IsNullOrWhiteSpace(dto.SectionName))
                throw new ArgumentException($"Section name is required at index {i}.", nameof(dtos));

            if (string.IsNullOrWhiteSpace(dto.Text))
                throw new ArgumentException($"Question text is required at index {i}.", nameof(dtos));

            if (dto.Type != QuestionType.TextBox &&
                (dto.AnswerOptions == null || dto.AnswerOptions.Count == 0))
            {
                throw new InvalidOperationException(
                    $"Non-TextBox question at index {i} must include answer options.");
            }
        }

        var sectionNames = dtoList
            .Select(d => d.SectionName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sections = await _context.SDSSections
            .Where(s => sectionNames.Contains(s.Name))
            .ToListAsync();

        var sectionByName = sections.ToDictionary(
            s => s.Name,
            s => s,
            StringComparer.OrdinalIgnoreCase);

        var missing = sectionNames.Where(n => !sectionByName.ContainsKey(n)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Sections not found: {string.Join(", ", missing)}");

        var entities = new List<SDSQuestion>(dtoList.Count);

        foreach (var dto in dtoList)
        {
            var section = sectionByName[dto.SectionName.Trim()];

            var question = new SDSQuestion
            {
                SectionId = section.Id,
                Text = dto.Text.Trim(),
                Type = dto.Type,
                AnswerOptions = new List<SDSAnswerOption>()
            };

            if (dto.Type != QuestionType.TextBox)
            {
                foreach (var opt in dto.AnswerOptions)
                {
                    if (opt == null) continue;

                    var value = string.IsNullOrWhiteSpace(opt.Value)
                        ? null
                        : opt.Value.Trim().ToUpperInvariant();

                    question.AnswerOptions.Add(new SDSAnswerOption
                    {
                        Text = opt.Text?.Trim(),
                        Value = value
                    });
                }
            }

            entities.Add(question);
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.SDSQuestions.AddRange(entities);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return entities.Select(e => e.Id).ToList();
    }

    public async Task<string> CalculateHollandCodeAsync(int userId, int attemptNumber)
    {
        var validCodes = new HashSet<string> { "R", "A", "I", "S", "E", "C" };

        var responses = await _context.SDSResponses
            .AsNoTracking()
            .Where(r =>
                r.UserId == userId &&
                r.AttemptNumber == attemptNumber &&
                !string.IsNullOrWhiteSpace(r.SelectedValue))
            .Select(r => r.SelectedValue.Trim().ToUpper())
            .ToListAsync();

        if (responses.Count == 0)
            return string.Empty;

        var points = validCodes.ToDictionary(c => c, c => 0);

        foreach (var val in responses)
        {
            var letter = val.Last().ToString();
            if (!validCodes.Contains(letter))
                continue;

            var numberPart = val.Length > 1 ? val[..^1] : "";
            int score = 1;

            if (!string.IsNullOrWhiteSpace(numberPart) &&
                int.TryParse(numberPart, out int parsed))
            {
                score = parsed;
            }

            points[letter] += score;
        }

        var top3 = points
            .OrderByDescending(p => p.Value)
            .Take(3)
            .Select(p => p.Key)
            .ToList();

        return string.Join("", top3);
    }

    public async Task<bool> SaveAIFeedbackAsync(int userId, string aiFeedback)
    {
        if (userId <= 0)
            throw new ArgumentException("Invalid userId.");

        if (string.IsNullOrWhiteSpace(aiFeedback))
            throw new ArgumentException("AIFeedback is required.");

        var latestResult = await _context.SDSResults
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.AttemptNumber)
            .FirstOrDefaultAsync();

        if (latestResult == null)
            return false; 

        latestResult.AIFeedback = aiFeedback.Trim();

        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> DeleteSDSQuestion(int questionId)
    {
        // 1. Load question
        var question = await _context.SDSQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            return false;

        // 2. Delete responses for this question
        await _context.SDSResponses
            .Where(r => r.QuestionId == questionId)
            .ExecuteDeleteAsync();

        // 3. Delete answer options
        await _context.SDSAnswerOptions
            .Where(o => o.QuestionId == questionId)
            .ExecuteDeleteAsync();

        // 4. Delete the question
        _context.SDSQuestions.Remove(question);
        await _context.SaveChangesAsync();

        return true;
    }
    public async Task<object> DeleteLastIncompleteSDSAsync(int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("Invalid userId.");

        // ?? Get latest incomplete attempt
        var attempt = await _context.SDSResults
            .Where(r => r.UserId == userId && !r.IsCompleted)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (attempt == null)
        {
            return new
            {
                Success = false,
                Message = "No incomplete SDS attempt found."
            };
        }

        // ?? Delete related responses first (if no cascade)
        await _context.SDSResponses
            .Where(r => r.SDSResultId == attempt.Id)
            .ExecuteDeleteAsync();

        // ?? Delete attempt
        _context.SDSResults.Remove(attempt);

        await _context.SaveChangesAsync();

        return new
        {
            Success = true,
            Message = "Incomplete SDS attempt deleted successfully."
        };
    }







    public async Task<IReadOnlyList<object>> GetAllSdsResultsAsync()
    {
        var results = await _context.SDSResults
            .AsNoTracking()
            .Include(r => r.User)
            .OrderBy(r => r.User.Email)
            .ThenBy(r => r.AttemptNumber)
            .Select(r => new
            {
                ResultId = r.Id,
                UserId = r.UserId,
                UserEmail = r.User.Email,
                HollandCode = r.HollandCode,
                AIFeedback = r.AIFeedback,
                AttemptNumber = r.AttemptNumber
            })
            .ToListAsync();

        return results;
    }

    public async Task<IReadOnlyList<object>> GetSdsResultsByUserId(int userId)
    {
        var results = await _context.SDSResults
            .AsNoTracking()
            .Where(r => r.UserId == userId) 
            .Include(r => r.User)
            .OrderBy(r => r.AttemptNumber)
            .Select(r => new
            {
                ResultId = r.Id,
                UserId = r.UserId,
                UserEmail = r.User.Email,
                HollandCode = r.HollandCode,
                AIFeedback = r.AIFeedback,
                AttemptNumber = r.AttemptNumber
            })
            .ToListAsync();

        return results;
    }

    public async Task<SdsExcelExportResult> ExportExcelAsync(
        SdsExportQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!SdsExcelExportHelper.TryParseCompletion(query.Completion, out var completion))
            throw new ArgumentException("Completion must be 'all', 'complete', or 'incomplete'.");

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate)
            throw new ArgumentException("FromDate must be on or before ToDate.");

        var from = query.FromDate;
        var toExclusive = SdsExcelExportHelper.ToExclusiveEnd(query.ToDate);

        var q = _context.SDSResults
            .AsNoTracking()
            .Include(r => r.User)
            .AsQueryable();

        if (completion == SdsExportCompletionFilter.Complete)
            q = q.Where(r => r.IsCompleted);
        else if (completion == SdsExportCompletionFilter.Incomplete)
            q = q.Where(r => !r.IsCompleted);

        if (from.HasValue)
        {
            var fromVal = from.Value;
            q = q.Where(r =>
                (r.IsCompleted && r.CompletedAt != null ? r.CompletedAt.Value : r.CreatedAt) >= fromVal);
        }

        if (toExclusive.HasValue)
        {
            var toVal = toExclusive.Value;
            q = q.Where(r =>
                (r.IsCompleted && r.CompletedAt != null ? r.CompletedAt.Value : r.CreatedAt) < toVal);
        }

        var rows = await q
            .OrderBy(r => r.User.Email)
            .ThenBy(r => r.AttemptNumber)
            .Select(r => new SdsExportRow
            {
                FirstName = r.User.FirstName,
                LastName = r.User.LastName,
                Email = r.User.Email,
                HollandCode = r.HollandCode,
                IsCompleted = r.IsCompleted,
                AttemptNumber = r.AttemptNumber,
                StartedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return SdsExcelExportHelper.BuildWorkbook(rows, from, query.ToDate, completion);
    }
}
