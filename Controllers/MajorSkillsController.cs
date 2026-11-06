using FrancProject.Dto;
using FrancProject.Extensions;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MajorSkillsController : ControllerBase
    {
        private readonly IMajorSkillsService _repo;
        private readonly IJobMatchingSearchLogger _searchLogger;

        public MajorSkillsController(IMajorSkillsService repo, IJobMatchingSearchLogger searchLogger)
        {
            _repo = repo;
            _searchLogger = searchLogger;
        }


        [HttpGet("check")]
        public async Task<IActionResult> Check(string searchKey)
        {
            var result = await _repo.CheckAsync(searchKey);
            return Ok(result);
        }

        [HttpPost("store")]
        public async Task<IActionResult> Store([FromBody] StoreMajorSkillsRequestDto request)
        {
            await _repo.StoreAsync(request);

            await _searchLogger.TryLogMajorSkillsSearchAsync(
                User.GetUserId(),
                request.Faculty,
                request.Major,
                request.Country);

            return Ok(new { message = "Stored successfully." });
        }
    }
}
