using System.Globalization;
using System.IO;
using DocxTemplater;
using FrancProject.Data;
using FrancProject.Dto;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services;

public class MockInterviewReportDocumentService
{
    private const string TemplateFileName = "MockInterviewTemplateTest.docx";

    private readonly DataContext _context;
    private readonly IWebHostEnvironment _env;

    public MockInterviewReportDocumentService(DataContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<(byte[] Content, string FileName)> GenerateReportDocumentAsync(
        EvaluationReportDto report,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            throw new ArgumentException("User not found.");

        var answerIds = report.Answers.Select(a => a.AnswerId).ToList();

        var evaluatorName = await _context.EvaluateQuestions
            .AsNoTracking()
            .Where(e => answerIds.Contains(e.AnswerId))
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.Evaluator.FirstName + " " + e.Evaluator.LastName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var model = BuildTemplateModel(report, user, evaluatorName);
        var templatePath = Path.Combine(_env.ContentRootPath, "Templates", TemplateFileName);

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Report template not found at '{templatePath}'.");

        using var templateStream = File.OpenRead(templatePath);
        using var template = new DocxTemplate(templateStream);

        template.BindModel("ds", model);

        using var outputStream = new MemoryStream();
        template.Save(outputStream);
        var content = outputStream.ToArray();

        var safeCandidateName = string.Join("_", (user.FirstName + " " + user.LastName).Split(Path.GetInvalidFileNameChars()));
        var fileName = $"MockInterviewReport_{safeCandidateName}_{report.GeneratedAt:yyyyMMdd}.docx";

        return (content, fileName);
    }

    private static MockInterviewReportTemplateModel BuildTemplateModel(
        EvaluationReportDto report,
        FrancProject.Models.User user,
        string evaluatorName)
    {
        var answers = report.Answers ?? new List<AnswerEvaluationDto>();

        var commonQuestions = answers
            .Where(a => a.QuestionTitle?.StartsWith("Common Question", StringComparison.OrdinalIgnoreCase) == true)
            .Select(MapQuestionItem)
            .ToList();

        var technicalQuestions = answers
            .Where(a =>
                !string.IsNullOrWhiteSpace(a.QuestionTitle) &&
                !a.QuestionTitle.Contains("Common", StringComparison.OrdinalIgnoreCase) &&
                !a.QuestionTitle.Contains("Candidate", StringComparison.OrdinalIgnoreCase))
            .Select(MapQuestionItem)
            .ToList();

        var candidateQuestions = answers
            .Where(a => a.QuestionTitle?.StartsWith("Candidate Question", StringComparison.OrdinalIgnoreCase) == true)
            .Select(MapQuestionItem)
            .ToList();

        return new MockInterviewReportTemplateModel
        {
            CandidateName = $"{user.FirstName} {user.LastName}".Trim(),
            CandidateId = GetCandidateIdFromEmail(user.Email),
            InterviewDate = report.GeneratedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EvaluatorName = evaluatorName,
            OverallRating = report.OverallRating?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-",
            SummaryComment = report.SummaryComment ?? string.Empty,
            SkillA1 = GetSkillRating(report.SkillScores, "A1"),
            SkillA2 = GetSkillRating(report.SkillScores, "A2"),
            SkillB1 = GetSkillRating(report.SkillScores, "B1"),
            SkillB2 = GetSkillRating(report.SkillScores, "B2"),
            SkillC = GetSkillRating(report.SkillScores, "C"),
            SkillD = GetSkillRating(report.SkillScores, "D"),
            SkillE = GetSkillRating(report.SkillScores, "E"),
            CommonQuestions = commonQuestions,
            TechnicalQuestions = technicalQuestions,
            CandidateQuestions = candidateQuestions
        };
    }

    private static ReportQuestionItemModel MapQuestionItem(AnswerEvaluationDto answer)
    {
        return new ReportQuestionItemModel
        {
            QuestionTitle = answer.QuestionTitle ?? string.Empty,
            Comment = answer.Comment ?? string.Empty,
            Tips = answer.Tips ?? string.Empty,
            Rating = answer.Rating ?? 0
        };
    }

    private static string GetCandidateIdFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        const string domain = "@ua.edu.lb";
        var domainIndex = email.IndexOf(domain, StringComparison.OrdinalIgnoreCase);
        if (domainIndex > 0)
            return email[..domainIndex];

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static string GetSkillRating(IEnumerable<ReportSkillScoreDto> skillScores, string skillCode)
    {
        var rating = skillScores?
            .FirstOrDefault(s => string.Equals(s.SkillCode, skillCode, StringComparison.OrdinalIgnoreCase))
            ?.Rating;

        return rating?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }

    private sealed class MockInterviewReportTemplateModel
    {
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string InterviewDate { get; set; } = string.Empty;
        public string EvaluatorName { get; set; } = string.Empty;
        public string OverallRating { get; set; } = string.Empty;
        public string SummaryComment { get; set; } = string.Empty;
        public string SkillA1 { get; set; } = "-";
        public string SkillA2 { get; set; } = "-";
        public string SkillB1 { get; set; } = "-";
        public string SkillB2 { get; set; } = "-";
        public string SkillC { get; set; } = "-";
        public string SkillD { get; set; } = "-";
        public string SkillE { get; set; } = "-";
        public List<ReportQuestionItemModel> CommonQuestions { get; set; } = new();
        public List<ReportQuestionItemModel> TechnicalQuestions { get; set; } = new();
        public List<ReportQuestionItemModel> CandidateQuestions { get; set; } = new();
    }

    private sealed class ReportQuestionItemModel
    {
        public string QuestionTitle { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Tips { get; set; } = string.Empty;
        public int Rating { get; set; }
    }
}
