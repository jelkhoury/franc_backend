using FrancProject.Dto;

namespace FrancProject.Interfaces;

public interface IEvaluationService
{
    Task EvaluateQuestionAsync(int answerId, int evaluatorId, int rating, string? comment, string? tips = null);
    Task<EvaluationReportDto> CreateEvaluationReportWithEvaluationsAsync(
        int userId,
        List<int> answerIds,
        string? summaryComment = null,
        List<ReportSkillScoreInputDto>? skillScores = null);
    Task EvaluateAnswersAsync(int evaluatorId, List<EvaluateAnswerDto> evaluations);
    Task IncreaseMockAttemptsAsync(int userId);
    Task<List<EvaluationReportDto>> GetReportsByUserIdAsync(int userId);
    Task<MockInterviewEvaluationsDto?> GetEvaluationsByMockInterviewIdAsync(int mockInterviewId);
}
