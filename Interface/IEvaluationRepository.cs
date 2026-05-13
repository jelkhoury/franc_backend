using System.Collections.Generic;
using System.Threading.Tasks;
using FrancProject.Models;
using FrancProject.Dto;

public interface IEvaluationRepository
{
    Task EvaluateQuestionAsync(int answerId, int evaluatorId, int rating, string? comment);
    Task<EvaluationReportDto> CreateEvaluationReportWithEvaluationsAsync(int userId, List<int> answerIds, string? summaryComment = null);
    Task EvaluateAnswersAsync(int evaluatorId, List<EvaluateAnswerDto> evaluations);
    Task IncreaseMockAttemptsAsync(int userId);
    Task<List<EvaluationReportDto>> GetReportsByUserIdAsync(int userId);



}
