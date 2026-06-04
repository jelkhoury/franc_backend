using FrancProject.Dto;
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

        public MajorSkillsController(IMajorSkillsService repo)
        {
            _repo = repo;
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
            return Ok(new { message = "Stored successfully." });
        }
    }
}
