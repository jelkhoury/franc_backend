using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrancProject.Dto;
using FrancProject.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("api/sds")]
[Authorize]
public class SdsController : ControllerBase
{
    private readonly ISdsRepository _repo;

    public SdsController(ISdsRepository sdsRepository)
    {
        _repo = sdsRepository;
    }

    // ---------------------------------------
    // CREATE SECTION
    // ---------------------------------------
    [HttpPost("create-section")]
    public async Task<IActionResult> CreateSection([FromBody] CreateSDSSectionDto dto)
    {
        try
        {
            var sectionId = await _repo.CreateSectionAsync(dto);
            return Ok(new { message = "Section created successfully.", sectionId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error creating section: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET ALL SECTIONS WITH QUESTIONS
    // ---------------------------------------
    [HttpGet("get-sections")]
    public async Task<IActionResult> GetAllSections()
    {
        try
        {
            var sections = await _repo.GetAllSectionsWithQuestionsAsync();
            return Ok(sections);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching sections: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // CREATE SINGLE QUESTION
    // ---------------------------------------
    [HttpPost("create-question")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateSDSQuestionWithOptionsDto dto)
    {
        try
        {
            var questionId = await _repo.CreateQuestionWithOptionsAsync(dto);
            return Ok(new { message = "Question created successfully.", questionId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error creating question: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // CREATE MULTIPLE QUESTIONS
    // ---------------------------------------
    [HttpPost("create-questions")]
    public async Task<IActionResult> CreateQuestions([FromBody] List<CreateSDSQuestionWithOptionsDto> dtos)
    {
        try
        {
            var ids = await _repo.CreateQuestionsWithOptionsAsync(dtos);
            return Ok(new { message = "Questions created successfully.", count = ids.Count, ids });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error creating questions: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET QUESTIONS BY SECTION
    // ---------------------------------------
    [HttpGet("get-questions-by-section")]
    public async Task<IActionResult> GetQuestionsBySection([FromQuery] int sectionId)
    {
        try
        {
            var questions = await _repo.GetQuestionsBySectionAsync(sectionId);
            return Ok(questions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching questions: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // SUBMIT RESPONSES
    // ---------------------------------------
    [HttpPost("submit-responses")]
    public async Task<IActionResult> SubmitResponses([FromBody] SubmitSDSResponsesDto dto)
    {
        try
        {
            var result = await _repo.SaveUserResponsesAsync(dto);
            return Ok(new { message = "Responses submitted successfully.", result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error submitting responses: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET HOLLAND POINTS BY SECTION
    // ---------------------------------------
    [HttpGet("get-holland-points")]
    public async Task<IActionResult> GetHollandPoints([FromQuery] int userId)
    {
        try
        {
            var points = await _repo.CalculateHollandPointsBySectionNameAsync(userId);
            return Ok(new { message = "Holland points fetched successfully.", data = points });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching Holland points: {ex.Message}" });
        }
    }


    // ---------------------------------------
    // GET HOLLAND POINTS FOR SPECIFIC ATTEMPT
    // ---------------------------------------
    [HttpGet("get-holland-points-by-attempt")]
    public async Task<IActionResult> GetHollandPointsByAttempt([FromQuery] int userId, [FromQuery] int attemptNumber)
    {
        try
        {
            var points = await _repo.CalculateHollandPointsBySectionNameForAttemptAsync(userId, attemptNumber);
            return Ok(new
            {
                message = "Holland points fetched successfully.",
                attempt = attemptNumber,
                data = points
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching Holland points for attempt {attemptNumber}: {ex.Message}" });
        }
    }


    // ---------------------------------------
    // GET ALL RESPONSES FOR USER
    // ---------------------------------------
    [HttpGet("get-user-responses")]
    public async Task<IActionResult> GetUserResponses([FromQuery] int userId)
    {
        try
        {
            var responses = await _repo.GetUserResponsesAsync(userId);
            return Ok(responses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching responses: {ex.Message}" });
        }
    }

    [HttpGet("SDSResults")]
    public async Task<IActionResult> GetAllResults()
    {
        try
        {
            var results = await _repo.GetAllSdsResultsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error fetching SDS results: {ex.Message}" });
        }
    }

    [HttpPost("save-ai-feedback")]
    public async Task<IActionResult> SaveAIFeedback([FromBody] SaveAIFeedbackDto dto)
    {
        try
        {
            if (dto == null)
                return BadRequest(new { error = "Invalid request body." });

            var saved = await _repo.SaveAIFeedbackAsync(dto.UserId, dto.AIFeedback);

            if (!saved)
                return NotFound(new { error = "No SDS result found for this user." });

            return Ok(new { message = "AI feedback saved successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error saving AI feedback: {ex.Message}" });
        }
    }

    [HttpDelete("delete-question")]
    public async Task<IActionResult> DeleteQuestion([FromQuery] int questionId)
    {
        try
        {
            if (questionId <= 0)
                return BadRequest(new { error = "Invalid questionId." });

            var deleted = await _repo.DeleteSDSQuestion(questionId);

            if (!deleted)
                return NotFound(new { error = $"Question with ID {questionId} not found." });

            return Ok(new { message = "Question deleted successfully.", questionId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error deleting question: {ex.Message}" });
        }
    }





}
