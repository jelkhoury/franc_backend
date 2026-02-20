using FrancProject.Dto.FrancProject.DTOs;
using FrancProject.DTOs;
using FrancProject.Models;

namespace FrancProject.Interfaces
{
    public interface IJobSearchRepository
    {
        Task<List<JobPostResponseDto>> CheckSearchAsync(string searchKey);
        Task StoreSearchWithJobsAsync(
           string searchKey,
           string queryText,
           string locationUsed,
           List<JobPostDto> jobs);
    }
}
