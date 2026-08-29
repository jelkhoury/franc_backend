using FrancProject.Dto;
using FrancProject.Models;

namespace FrancProject.Interfaces;

public interface ISdsService
{
    Task<int> CreateSectionAsync(CreateSDSSectionDto dto);
    Task<IReadOnlyList<SDSSection>> GetAllSectionsWithQuestionsAsync();
    Task<int> CreateQuestionWithOptionsAsync(CreateSDSQuestionWithOptionsDto dto);
    Task<IReadOnlyList<SDSQuestion>> GetQuestionsBySectionAsync(int sectionId);
    Task<object> SaveUserResponsesAsync(SubmitSDSResponsesDto dto);
    Task<IReadOnlyList<SDSResponse>> GetUserResponsesAsync(int userId);
    Task<IReadOnlyList<int>> CreateQuestionsWithOptionsAsync(IEnumerable<CreateSDSQuestionWithOptionsDto> dtos);
    Task<Dictionary<string, Dictionary<string, int>>> CalculateHollandPointsBySectionNameAsync(int userId);
    Task<IReadOnlyList<object>> GetAllSdsResultsAsync();
    Task<IReadOnlyList<object>> GetSdsResultsByUserId(int userId);
    Task<Dictionary<string, Dictionary<string, int>>> CalculateHollandPointsBySectionNameForAttemptAsync(int userId, int attemptNumber);
    Task<bool> SaveAIFeedbackAsync(int userId, string aiFeedback);
    Task<bool> DeleteSDSQuestion(int questionId);
    Task<object> DeleteLastIncompleteSDSAsync(int userId);
    Task<SdsExcelExportResult> ExportExcelAsync(SdsExportQueryDto query, CancellationToken cancellationToken = default);
}
