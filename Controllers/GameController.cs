using FrancProject.DTOs;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FrancProject.Controllers
{
    [ApiController]
    [Route("api/game")]
    [Authorize]
    public class GameController : ControllerBase
    {
        private readonly IGameQuizService _gameQuiz;
        private readonly IGameQuestionImportService _questionImport;

        public GameController(IGameQuizService gameQuiz, IGameQuestionImportService questionImport)
        {
            _gameQuiz = gameQuiz;
            _questionImport = questionImport;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Badges, unlocked levels, and total points for the current user.</summary>
        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            try
            {
                var dto = await _gameQuiz.GetProgressAsync(GetUserId());
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Start a new quiz run for a level.</summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartQuiz([FromBody] StartGameSessionRequestDto request)
        {
            try
            {
                var dto = await _gameQuiz.StartSessionAsync(GetUserId(), request);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Get current quiz state (questions, scores, abilities left).</summary>
        [HttpGet("session/{sessionId:long}")]
        public async Task<IActionResult> GetSessionState(long sessionId)
        {
            try
            {
                var dto = await _gameQuiz.GetSessionAsync(GetUserId(), sessionId);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Submit the chosen option for one question slot.</summary>
        [HttpPost("session/{sessionId:long}/answer/{answerId:long}")]
        public async Task<IActionResult> AnswerQuestion(long sessionId, long answerId,
            [FromBody] SubmitGameAnswerRequestDto request)
        {
            try
            {
                var dto = await _gameQuiz.SubmitAnswerAsync(GetUserId(), sessionId, answerId, request);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Use a hint, skip, fifty-fifty, etc. on one question slot.</summary>
        [HttpPost("session/{sessionId:long}/ability/{answerId:long}")]
        public async Task<IActionResult> UseAbility(long sessionId, long answerId,
            [FromBody] UseGameAbilityRequestDto request)
        {
            try
            {
                var dto = await _gameQuiz.UseAbilityAsync(GetUserId(), sessionId, answerId, request);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Finalize the quiz when all questions are done (or to sync server state).</summary>
        [HttpPost("session/{sessionId:long}/finish")]
        public async Task<IActionResult> FinishQuiz(long sessionId)
        {
            try
            {
                var dto = await _gameQuiz.FinishSessionAsync(GetUserId(), sessionId);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Admin-only: import quiz questions from an .xlsx workbook (sheets "Level 1" … "Level 5").</summary>
        [HttpPost("questions/import")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> ImportQuestionsFromExcel(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .xlsx (Excel workbook) files are supported." });

            await using var stream = file.OpenReadStream();
            var result = await _questionImport.ImportFromExcelAsync(stream, cancellationToken);
            return Ok(result);
        }
    }
}
