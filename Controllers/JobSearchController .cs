using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JobSearchController : ControllerBase
    {
        private readonly IJobSearchService _repo;

        public JobSearchController(IJobSearchService repo)
        {
            _repo = repo;
        }

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

            return Ok(new { message = "Stored successfully (48h TTL applied)." });
        }
    }

   
}
