using FrancProject.Dto;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers;

[ApiController]
[Route("api/sds")]
[Authorize]
public class SdsController : ControllerBase
{
    private readonly ISdsService _repo;

    public SdsController(ISdsService sdsService)
    {
        _repo = sdsService;
    }

    [HttpPost("create-section")]
    public async Task<IActionResult> CreateSection([FromBody] CreateSDSSectionDto dto)
    {
        var sectionId = await _repo.CreateSectionAsync(dto);
        return Ok(new { message = "Section created successfully.", sectionId });
    }

    [HttpGet("get-sections")]
    public async Task<IActionResult> GetAllSections()
    {
        var sections = await _repo.GetAllSectionsWithQuestionsAsync();
        return Ok(sections);
    }

    [HttpPost("create-question")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateSDSQuestionWithOptionsDto dto)
    {
        var questionId = await _repo.CreateQuestionWithOptionsAsync(dto);
        return Ok(new { message = "Question created successfully.", questionId });
    }

    [HttpPost("create-questions")]
    public async Task<IActionResult> CreateQuestions([FromBody] List<CreateSDSQuestionWithOptionsDto> dtos)
    {
        var ids = await _repo.CreateQuestionsWithOptionsAsync(dtos);
        return Ok(new { message = "Questions created successfully.", count = ids.Count, ids });
    }

    [HttpGet("get-questions-by-section")]
    public async Task<IActionResult> GetQuestionsBySection([FromQuery] int sectionId)
    {
        var questions = await _repo.GetQuestionsBySectionAsync(sectionId);
        return Ok(questions);
    }

    [HttpPost("submit-responses")]
    public async Task<IActionResult> SubmitResponses([FromBody] SubmitSDSResponsesDto dto)
    {
        var result = await _repo.SaveUserResponsesAsync(dto);
        return Ok(new { message = "Responses submitted successfully.", result });
    }

    [HttpGet("get-holland-points")]
    public async Task<IActionResult> GetHollandPoints([FromQuery] int userId)
    {
        var points = await _repo.CalculateHollandPointsBySectionNameAsync(userId);
        return Ok(new { message = "Holland points fetched successfully.", data = points });
    }

    [HttpGet("get-holland-points-by-attempt")]
    public async Task<IActionResult> GetHollandPointsByAttempt([FromQuery] int userId, [FromQuery] int attemptNumber)
    {
        var points = await _repo.CalculateHollandPointsBySectionNameForAttemptAsync(userId, attemptNumber);
        return Ok(new
        {
            message = "Holland points fetched successfully.",
            attempt = attemptNumber,
            data = points
        });
    }

    [HttpGet("get-user-responses")]
    public async Task<IActionResult> GetUserResponses([FromQuery] int userId)
    {
        var responses = await _repo.GetUserResponsesAsync(userId);
        return Ok(responses);
    }

    [HttpGet("SDSResults")]
    public async Task<IActionResult> GetAllResults()
    {
        var results = await _repo.GetAllSdsResultsAsync();
        return Ok(results);
    }

    [HttpGet("GetUserSDSResults")]
    public async Task<IActionResult> GetUserSDSResults(int userId)
    {
        var results = await _repo.GetSdsResultsByUserId(userId);
        return Ok(results);
    }

    [HttpPost("save-ai-feedback")]
    public async Task<IActionResult> SaveAIFeedback([FromBody] SaveAIFeedbackDto dto)
    {
        if (dto == null)
            return BadRequest(new { error = "Invalid request body." });

        var saved = await _repo.SaveAIFeedbackAsync(dto.UserId, dto.AIFeedback);

        if (!saved)
            return NotFound(new { error = "No SDS result found for this user." });

        return Ok(new { message = "AI feedback saved successfully." });
    }

    [HttpDelete("delete-question")]
    public async Task<IActionResult> DeleteQuestion([FromQuery] int questionId)
    {
        if (questionId <= 0)
            return BadRequest(new { error = "Invalid questionId." });

        var deleted = await _repo.DeleteSDSQuestion(questionId);

        if (!deleted)
            return NotFound(new { error = $"Question with ID {questionId} not found." });

        return Ok(new { message = "Question deleted successfully.", questionId });
    }

    [HttpDelete("delete-last-incomplete")]
    public async Task<IActionResult> DeleteLastIncomplete([FromQuery] int userId)
    {
        if (userId <= 0)
            return BadRequest(new { error = "Invalid userId." });

        var result = await _repo.DeleteLastIncompleteSDSAsync(userId);
        return Ok(result);
    }
}
