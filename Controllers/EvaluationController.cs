using FrancProject.Dto;
using FrancProject.Extensions;
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

    // --------------------------------------------------
    // ADMIN: SCORING AND QUOTA
    // The evaluator is taken from the token, never from the request body,
    // so an evaluation cannot be attributed to somebody else.
    // --------------------------------------------------

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateQuestion([FromBody] EvaluateRequest req)
    {
        await _repo.EvaluateQuestionAsync(req.AnswerId, User.GetUserId(), req.Rating, req.Comment, req.Tips);
        return Ok(new { message = "Evaluation saved successfully." });
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpPost("evaluate-multiple")]
    public async Task<IActionResult> EvaluateMultiple([FromBody] EvaluateMultipleRequest req)
    {
        await _repo.EvaluateAnswersAsync(User.GetUserId(), req.Evaluations);
        return Ok(new { message = "All evaluations processed successfully." });
    }

    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
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

    /// <summary>
    /// Grants a user another mock interview attempt. Admin-only: this is the quota that
    /// <c>CanUserPerformAction</c> enforces, so a user must not be able to top up their own.
    /// </summary>
    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [HttpPost("increase-attempt")]
    public async Task<IActionResult> IncreaseAttempt([FromQuery] int userId)
    {
        await _repo.IncreaseMockAttemptsAsync(userId);
        return Ok(new { message = "Mock attempt increased successfully.", userId });
    }

    // --------------------------------------------------
    // AUTHENTICATED: OWN REPORTS
    // --------------------------------------------------

    [HttpGet("GetUserInterviewsReports")]
    public async Task<IActionResult> GetReportsByUser([FromQuery] int? userId = null)
    {
        var reports = await _repo.GetReportsByUserIdAsync(User.ResolveAccessibleUserId(userId));
        return Ok(reports);
    }

    [HttpGet("GetEvaluationsByMockInterviewId")]
    public async Task<IActionResult> GetEvaluationsByMockInterview([FromQuery] int mockInterviewId)
    {
        if (!User.IsAdmin() &&
            !await _repo.IsMockInterviewOwnedByUserAsync(mockInterviewId, User.GetUserId()))
        {
            return Forbid();
        }

        var result = await _repo.GetEvaluationsByMockInterviewIdAsync(mockInterviewId);

        if (result == null)
            return NotFound(new { error = "Mock interview not found." });

        return Ok(result);
    }
}
