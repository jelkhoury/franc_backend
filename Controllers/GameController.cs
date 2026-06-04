using FrancProject.Dto;
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
        private readonly IFileUploadSecurityService _uploadSecurity;

        public GameController(
            IGameQuizService gameQuiz,
            IGameQuestionImportService questionImport,
            IFileUploadSecurityService uploadSecurity)
        {
            _gameQuiz = gameQuiz;
            _questionImport = questionImport;
            _uploadSecurity = uploadSecurity;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            var dto = await _gameQuiz.GetProgressAsync(GetUserId());
            return Ok(dto);
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartQuiz([FromBody] StartGameSessionRequestDto request)
        {
            var dto = await _gameQuiz.StartSessionAsync(GetUserId(), request);
            return Ok(dto);
        }

        [HttpGet("session/{sessionId:long}")]
        public async Task<IActionResult> GetSessionState(long sessionId)
        {
            var dto = await _gameQuiz.GetSessionAsync(GetUserId(), sessionId);
            return Ok(dto);
        }

        [HttpGet("session/{sessionId:long}/hints")]
        public async Task<IActionResult> GetSessionHints(long sessionId)
        {
            var dto = await _gameQuiz.GetSessionHintsAsync(GetUserId(), sessionId);
            return Ok(dto);
        }

        [HttpPost("session/{sessionId:long}/answer/{answerId:long}")]
        public async Task<IActionResult> AnswerQuestion(long sessionId, long answerId,
            [FromBody] SubmitGameAnswerRequestDto request)
        {
            var dto = await _gameQuiz.SubmitAnswerAsync(GetUserId(), sessionId, answerId, request);
            return Ok(dto);
        }

        [HttpPost("session/{sessionId:long}/ability/{answerId:long}")]
        public async Task<IActionResult> UseAbility(long sessionId, long answerId,
            [FromBody] UseGameAbilityRequestDto request)
        {
            var dto = await _gameQuiz.UseAbilityAsync(GetUserId(), sessionId, answerId, request);
            return Ok(dto);
        }

        [HttpPost("session/{sessionId:long}/finish")]
        public async Task<IActionResult> FinishQuiz(long sessionId)
        {
            var dto = await _gameQuiz.FinishSessionAsync(GetUserId(), sessionId);
            return Ok(dto);
        }

        [HttpPost("questions/import")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportQuestionsFromExcel(
            [FromForm] ImportQuestionsFormDto model,
            CancellationToken cancellationToken)
        {
            if (model.File == null || model.File.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            await _uploadSecurity.ValidateAsync(model.File, FileUploadCategory.Spreadsheet, cancellationToken);

            await using var stream = model.File.OpenReadStream();
            var result = await _questionImport.ImportFromExcelAsync(stream, cancellationToken);
            return Ok(result);
        }
    }
}
