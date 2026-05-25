using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrancProject.Dto;
using FrancProject.Interface;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

[ApiController]
[Route("api/evaluation")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationRepository _repo;
    private readonly MockInterviewReportDocumentService _reportDocumentService;

    public EvaluationController(
        IEvaluationRepository evaluationRepository,
        MockInterviewReportDocumentService reportDocumentService)
    {
        _repo = evaluationRepository;
        _reportDocumentService = reportDocumentService;
    }

    // ---------------------------------------
    // EVALUATE SINGLE QUESTION
    // ---------------------------------------
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateQuestion([FromBody] EvaluateRequest req)
    {
        try
        {
            await _repo.EvaluateQuestionAsync(req.AnswerId, req.EvaluatorId, req.Rating, req.Comment, req.Tips);
            return Ok(new { message = "Evaluation saved successfully." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // EVALUATE MULTIPLE ANSWERS
    // ---------------------------------------
    [HttpPost("evaluate-multiple")]
    public async Task<IActionResult> EvaluateMultiple([FromBody] EvaluateMultipleRequest req)
    {
        try
        {
            await _repo.EvaluateAnswersAsync(req.EvaluatorId, req.Evaluations);
            return Ok(new { message = "All evaluations processed successfully." });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // CREATE EVALUATION REPORT
    // ---------------------------------------
    [HttpPost("create-report")]
    public async Task<IActionResult> CreateReport([FromBody] EvaluationReportRequest req)
    {
        try
        {
            var result = await _repo.CreateEvaluationReportWithEvaluationsAsync(
                req.UserId, req.AnswerIds, req.SummaryComment, req.SkillScores);

            var (content, fileName) = await _reportDocumentService.GenerateReportDocumentAsync(result, req.UserId);

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error generating report: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // INCREASE MOCK ATTEMPTS
    // ---------------------------------------
    [HttpPost("increase-attempt")]
    public async Task<IActionResult> IncreaseAttempt([FromQuery] int userId)
    {
        try
        {
            await _repo.IncreaseMockAttemptsAsync(userId);
            return Ok(new { message = "Mock attempt increased successfully.", userId });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error increasing attempts: {ex.Message}" });
        }
    }

    [HttpGet("GetUserInterviewsReports")]
    public async Task<IActionResult> GetReportsByUser(int userId)
    {
        try
        {
            var reports = await _repo.GetReportsByUserIdAsync(userId);

            return Ok(reports);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "An error occurred while retrieving reports.",
                error = ex.Message 
            });
        }
    }

    [HttpGet("GetEvaluationsByMockInterviewId")]
    public async Task<IActionResult> GetEvaluationsByMockInterview(int mockInterviewId)
    {
        try
        {
            var result = await _repo.GetEvaluationsByMockInterviewIdAsync(mockInterviewId);

            if (result == null)
                return NotFound(new { error = "Mock interview not found." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "An error occurred while retrieving evaluations.",
                error = ex.Message
            });
        }
    }

}


public class EvaluationReportRequest
{
    public int UserId { get; set; }
    public List<int> AnswerIds { get; set; } = new List<int>();
    public string? SummaryComment { get; set; }
    public List<ReportSkillScoreInputDto> SkillScores { get; set; } = new();
}

public class EvaluateRequest
{
    public int AnswerId { get; set; }
    public int EvaluatorId { get; set; }
    public int Rating { get; set; } 
    public string? Comment { get; set; }
    public string? Tips { get; set; }
}

public class EvaluateAnswerDto
{
    public int AnswerId { get; set; }
    public int Rating { get; set; } 
    public string? Comment { get; set; }
    public string? Tips { get; set; }
}

public class EvaluateMultipleRequest
{
    public int EvaluatorId { get; set; }
    public List<EvaluateAnswerDto> Evaluations { get; set; } = new List<EvaluateAnswerDto>();
}
