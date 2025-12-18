using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Models;
using FrancProject.Interface;

public class SdsRepository : ISdsRepository
{
    private readonly DataContext _context;
    private readonly IConfiguration _configuration;

    public SdsRepository(DataContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
    public async Task<object> SaveUserResponsesAsync(SubmitSDSResponsesDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (dto.UserId <= 0)
            throw new ArgumentException("UserId is required.", nameof(dto));

        if (dto.Responses == null || dto.Responses.Count == 0)
            throw new ArgumentException("At least one response is required.", nameof(dto));

        // 1. Determine new attempt number
        int lastAttempt = await _context.SDSResponses
            .Where(r => r.UserId == dto.UserId)
            .MaxAsync(r => (int?)r.AttemptNumber) ?? 0;

        int newAttempt = lastAttempt + 1;

        // 2. Load question metadata for validation
        var questionIds = dto.Responses.Select(r => r.QuestionId).Distinct().ToList();

        var questions = await _context.SDSQuestions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Type })
            .ToDictionaryAsync(q => q.Id, q => q.Type);

        if (questions.Count != questionIds.Count)
            throw new InvalidOperationException("One or more QuestionIds do not exist.");

        // 3. Prepare response entities
        var now = DateTime.UtcNow;
        var entities = new List<SDSResponse>();

        foreach (var r in dto.Responses)
        {
            if (!questions.TryGetValue(r.QuestionId, out var qType))
                throw new InvalidOperationException($"Question {r.QuestionId} not found.");

            if ((qType == QuestionType.TextBox || qType == QuestionType.TextArea) &&
                string.IsNullOrWhiteSpace(r.CustomAnswer))
            {
                throw new InvalidOperationException($"Question {r.QuestionId} requires a text answer.");
            }

            entities.Add(new SDSResponse
            {
                UserId = dto.UserId,
                QuestionId = r.QuestionId,
                AttemptNumber = newAttempt,
                SelectedValue = string.IsNullOrWhiteSpace(r.SelectedValue)
                    ? null
                    : r.SelectedValue.Trim().ToUpperInvariant(),
                CustomAnswer = string.IsNullOrWhiteSpace(r.CustomAnswer)
                    ? null
                    : r.CustomAnswer.Trim(),
                SubmittedAt = now
            });
        }

        // 4. Save all responses
        await _context.SDSResponses.AddRangeAsync(entities);
        await _context.SaveChangesAsync();

        // 5. Compute Holland Code for this attempt
        var hollandCode = await CalculateHollandCodeAsync(dto.UserId, newAttempt);

        // 6. Create SDSResult record (IMPORTANT: do NOT set Responses list)
        var result = new SDSResult
        {
            UserId = dto.UserId,
            AttemptNumber = newAttempt,
            HollandCode = hollandCode,
            AIFeedback = null
        };

        _context.SDSResults.Add(result);
        await _context.SaveChangesAsync();

        // 7. Link responses to SDSResult (high performance Update)
        await _context.SDSResponses
            .Where(r => r.UserId == dto.UserId && r.AttemptNumber == newAttempt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.SDSResultId, result.Id)
            );

        // 8. Fetch final subset to return (IDs 362 & 363)
        var wantedIds = new[] { 362, 363 };
        var wantedResponses = await _context.SDSResponses
            .AsNoTracking()
            .Where(r => r.UserId == dto.UserId &&
                        r.AttemptNumber == newAttempt &&
                        wantedIds.Contains(r.QuestionId))
            .Select(r => new
            {
                r.QuestionId,
                r.SelectedValue,
                r.CustomAnswer,
                r.SubmittedAt
            })
            .ToListAsync();

        // 9. Return final result object
        return new
        {
            Attempt = newAttempt,
            HollandCode = hollandCode,
            SDSResultId = result.Id,
            Responses = wantedResponses
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

        // 1?? Get latest SDSResult (highest AttemptNumber)
        var latestResult = await _context.SDSResults
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.AttemptNumber)
            .FirstOrDefaultAsync();

        if (latestResult == null)
            return false; // no attempts exist

        // 2?? Save feedback
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



}
