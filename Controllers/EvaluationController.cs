using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrancProject.Models;
using FrancProject.Dto;
using System;

[ApiController]
[Route("api/[controller]")]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationRepository _evaluationRepository;

    public EvaluationController(IEvaluationRepository evaluationRepository)
    {
        _evaluationRepository = evaluationRepository;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateQuestion([FromBody] EvaluateRequest request)
    {
        try
        {
            await _evaluationRepository.EvaluateQuestionAsync(
                request.AnswerId,
                request.EvaluatorId,
                request.Rating,
                request.Comment
            );

            return Ok(new { message = "Evaluation saved successfully." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpPost("evaluate-multiple")]
    public async Task<IActionResult> EvaluateMultipleAnswers([FromBody] EvaluateMultipleRequest request)
    {
        try
        {
            await _evaluationRepository.EvaluateAnswersAsync(request.EvaluatorId, request.Evaluations);
            return Ok(new { message = "All evaluations processed successfully." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpPost("report")]
    public async Task<IActionResult> CreateEvaluationReport([FromBody] EvaluationReportRequest request)
    {
        try
        {
            var report = await _evaluationRepository.CreateEvaluationReportWithEvaluationsAsync(
                request.UserId,
                request.AnswerIds,
                request.SummaryComment
            );

            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    [HttpGet("can-do-mock/{userId}")]
    public async Task<IActionResult> CanUserDoMock(int userId)
    {
        try
        {
            var result = await _evaluationRepository.CanUserDoMockInterviewAsync(userId);
            return Ok(new { userId, canDoMock = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpPost("increase-attempt/{userId}")]
    public async Task<IActionResult> IncreaseMockAttempts(int userId)
    {
        try
        {
            await _evaluationRepository.IncreaseMockAttemptsAsync(userId);
            return Ok(new { message = "Mock attempt count increased successfully.", userId });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error increasing attempts: {ex.Message}" });
        }
    }

}


public class EvaluationReportRequest
{
    public int UserId { get; set; }
    public List<int> AnswerIds { get; set; } = new List<int>();
    public string? SummaryComment { get; set; }
}

public class EvaluateRequest
{
    public int AnswerId { get; set; }
    public int EvaluatorId { get; set; }
    public int Rating { get; set; } 
    public string? Comment { get; set; }
}

public class EvaluateAnswerDto
{
    public int AnswerId { get; set; }
    public int Rating { get; set; } 
    public string? Comment { get; set; }
}

public class EvaluateMultipleRequest
{
    public int EvaluatorId { get; set; }
    public List<EvaluateAnswerDto> Evaluations { get; set; } = new List<EvaluateAnswerDto>();
}
