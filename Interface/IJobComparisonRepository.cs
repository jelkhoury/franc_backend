using FrancProject.Dto;
using FrancProject.Models;

public interface IJobComparisonRepository
{
    // ===== Criteria =====
    Task<List<JobComparisonCriterion>> GetCriteriaAsync();
    Task<JobComparisonCriterion> CreateCriterionAsync(JobComparisonCriterion criterion);
    Task<List<JobComparisonCriterion>> BulkCreateCriteriaAsync(
        List<CreateJobComparisonCriterionDto> dtos);
    Task<JobComparisonCriterion?> UpdateCriterionAsync(int id, JobComparisonCriterion criterion);
    Task<bool> DeleteCriterionAsync(int id);

    // ===== Job Comparison =====
    Task<int> SaveJobComparisonAsync(int userId, SaveJobComparisonDto dto);
    Task<JobComparison?> GetJobComparisonAsync(int userId, int jobComparisonId);
    Task<List<JobComparison>> GetAllJobComparisonsAsync(int userId);
    Task<bool> DeleteJobComparisonAsync(int userId, int jobComparisonId);
    Task<JobComparison?> GetLatestIncompleteJobComparisonAsync(int userId);

}
