using FrancProject.Dto;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FrancProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JobSearchController : ControllerBase
    {
        private readonly IJobSearchService _repo;
        private readonly IJobMatchingSearchLogger _searchLogger;

        public JobSearchController(IJobSearchService repo, IJobMatchingSearchLogger searchLogger)
        {
            _repo = repo;
            _searchLogger = searchLogger;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("check")]
        public async Task<IActionResult> Check(string searchKey)
        {
            var jobs = await _repo.CheckSearchAsync(searchKey);
            return Ok(jobs);
        }

        [HttpPost("store")]
        public async Task<IActionResult> Store([FromBody] StoreSearchRequestDto request)
        {
            await _repo.StoreSearchWithJobsAsync(
                request.SearchKey,
                request.QueryText,
                request.LocationUsed,
                request.Jobs);

            await _searchLogger.TryLogOpportunitiesSearchAsync(
                GetUserId(),
                request.QueryText,
                request.LocationUsed,
                request.Jobs?.Count ?? 0);

            return Ok(new { message = "Stored successfully (48h TTL applied)." });
        }
    }
}
