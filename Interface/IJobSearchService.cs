using FrancProject.Dto;
using FrancProject.Models;

namespace FrancProject.Interfaces
{
    public interface IJobSearchService
    {
        Task<List<JobPostResponseDto>> CheckSearchAsync(string searchKey);
        Task StoreSearchWithJobsAsync(
           string searchKey,
           string queryText,
           string locationUsed,
           List<JobPostDto> jobs);
    }
}
