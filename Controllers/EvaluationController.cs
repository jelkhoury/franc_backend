using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers;

[ApiController]
[Route("api/evaluation")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _repo;
    private readonly MockInterviewReportDocumentService _reportDocumentService;

    public EvaluationController(
        IEvaluationService evaluationService,
        MockInterviewReportDocumentService reportDocumentService)
    {
        _repo = evaluationService;
        _reportDocumentService = reportDocumentService;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateQuestion([FromBody] EvaluateRequest req)
    {
        await _repo.EvaluateQuestionAsync(req.AnswerId, req.EvaluatorId, req.Rating, req.Comment, req.Tips);
        return Ok(new { message = "Evaluation saved successfully." });
    }

    [HttpPost("evaluate-multiple")]
    public async Task<IActionResult> EvaluateMultiple([FromBody] EvaluateMultipleRequest req)
    {
        await _repo.EvaluateAnswersAsync(req.EvaluatorId, req.Evaluations);
        return Ok(new { message = "All evaluations processed successfully." });
    }

    [HttpPost("create-report")]
    public async Task<IActionResult> CreateReport([FromBody] EvaluationReportRequest req)
    {
        var result = await _repo.CreateEvaluationReportWithEvaluationsAsync(
            req.UserId, req.AnswerIds, req.SummaryComment, req.SkillScores);

        var (content, fileName) = await _reportDocumentService.GenerateReportDocumentAsync(result, req.UserId);

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    [HttpPost("increase-attempt")]
    public async Task<IActionResult> IncreaseAttempt([FromQuery] int userId)
    {
        await _repo.IncreaseMockAttemptsAsync(userId);
        return Ok(new { message = "Mock attempt increased successfully.", userId });
    }

    [HttpGet("GetUserInterviewsReports")]
    public async Task<IActionResult> GetReportsByUser(int userId)
    {
        var reports = await _repo.GetReportsByUserIdAsync(userId);
        return Ok(reports);
    }

    [HttpGet("GetEvaluationsByMockInterviewId")]
    public async Task<IActionResult> GetEvaluationsByMockInterview(int mockInterviewId)
    {
        var result = await _repo.GetEvaluationsByMockInterviewIdAsync(mockInterviewId);

        if (result == null)
            return NotFound(new { error = "Mock interview not found." });

        return Ok(result);
    }
}
