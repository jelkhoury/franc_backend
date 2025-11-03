using Microsoft.AspNetCore.Mvc;
using FrancProject.Interface;
using FrancProject.Dto;
using FrancProject.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

[ApiController]
[Route("api/[controller]")]
public class SdsController : ControllerBase
{
    private readonly ISdsRepository _sdsRepository;

    public SdsController(ISdsRepository sdsRepository)
    {
        _sdsRepository = sdsRepository;
    }

    [HttpPost("sections")]
    public async Task<IActionResult> CreateSection([FromBody] CreateSDSSectionDto dto)
    {
        try
        {
            var sectionId = await _sdsRepository.CreateSectionAsync(dto);
            return Ok(new { message = "Section created successfully.", sectionId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating section: {ex.Message}");
        }
    }

    [HttpGet("sections")]
    public async Task<IActionResult> GetAllSections()
    {
        try
        {
            var sections = await _sdsRepository.GetAllSectionsWithQuestionsAsync();
            return Ok(sections);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching sections: {ex.Message}");
        }
    }

    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateSDSQuestionWithOptionsDto dto)
    {
        try
        {
            var questionId = await _sdsRepository.CreateQuestionWithOptionsAsync(dto);
            return Ok(new { message = "Question created successfully.", questionId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating question: {ex.Message}");
        }
    }

    [HttpPost("questions/bulk")]
    public async Task<IActionResult> CreateQuestions([FromBody] List<CreateSDSQuestionWithOptionsDto> dtos)
    {
        try
        {
            if (dtos == null || dtos.Count == 0)
                return BadRequest("At least one question is required.");

            var createdIds = await _sdsRepository.CreateQuestionsWithOptionsAsync(dtos);
            return Ok(new
            {
                message = "Questions created successfully.",
                count = createdIds.Count,
                questionIds = createdIds
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating questions: {ex.Message}");
        }
    }

    [HttpGet("sections/{sectionId}/questions")]
    public async Task<IActionResult> GetQuestionsBySection(int sectionId)
    {
        try
        {
            var questions = await _sdsRepository.GetQuestionsBySectionAsync(sectionId);
            return Ok(questions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching questions: {ex.Message}");
        }
    }


    [HttpPost("responses")]
    public async Task<IActionResult> SubmitResponses([FromBody] SubmitSDSResponsesDto dto)
    {
        try
        {
            var hollandCode = await _sdsRepository.SaveUserResponsesAsync(dto);
            return Ok(new { message = "Responses submitted successfully.", hollandCode });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error submitting responses: {ex.Message}");
        }
    }


    [HttpGet("responses/{userId}/holland-points")]
    public async Task<IActionResult> GetHollandPointsBySection(int userId)
    {
        try
        {
            if (userId <= 0)
                return BadRequest("Invalid user ID.");

            var pointsBySection = await _sdsRepository.CalculateHollandPointsBySectionNameAsync(userId);

            return Ok(new
            {
                message = "Holland points by section fetched successfully.",
                data = pointsBySection
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error calculating Holland points: {ex.Message}");
        }
    }



    [HttpGet("responses/{userId}")]
    public async Task<IActionResult> GetUserResponses(int userId)
    {
        try
        {
            var responses = await _sdsRepository.GetUserResponsesAsync(userId);
            return Ok(responses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching responses: {ex.Message}");
        }
    }
}
