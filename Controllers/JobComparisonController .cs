using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FrancProject.Controllers;

[ApiController]
[Route("api/jobcomparison")]
[Authorize]
public class JobComparisonController : ControllerBase
{
    private readonly IJobComparisonService _jobComparisonRepo;
    private readonly JobComparisonExcelService _excelService;

    public JobComparisonController(
        IJobComparisonService jobComparisonRepo,
        JobComparisonExcelService excelService)
    {
        _jobComparisonRepo = jobComparisonRepo;
        _excelService = excelService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("criteria")]
    public async Task<IActionResult> GetCriteria()
    {
        var criteria = await _jobComparisonRepo.GetCriteriaAsync();

        var result = criteria.Select(c => new JobComparisonCriterionDto
        {
            Id = c.Id,
            Name = c.Name,
            Section = c.Section,
            Category = c.Category,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder
        });

        return Ok(result);
    }

    [HttpPost("criteria/bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreateCriteria(
        [FromBody] List<CreateJobComparisonCriterionDto> dtos)
    {
        if (dtos == null || dtos.Count == 0)
            return BadRequest(new { error = "Criteria list cannot be empty." });

        var created = await _jobComparisonRepo.BulkCreateCriteriaAsync(dtos);

        return Ok(new
        {
            message = "Criteria created successfully.",
            count = created.Count,
            criteria = created.Select(c => new
            {
                c.Id,
                c.Name,
                c.Section,
                c.Category,
                c.DisplayOrder,
                c.IsActive
            })
        });
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveJobComparison([FromBody] SaveJobComparisonDto dto)
    {
        int userId = GetUserId();
        int jobComparisonId = await _jobComparisonRepo.SaveJobComparisonAsync(userId, dto);

        return Ok(new
        {
            message = "Job comparison saved successfully.",
            jobComparisonId
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJobComparison([FromRoute] int id)
    {
        int userId = GetUserId();

        var comparison = await _jobComparisonRepo.GetJobComparisonAsync(userId, id);

        if (comparison == null)
            return NotFound(new { message = "Job comparison not found." });

        var result = new JobComparisonDto
        {
            Id = comparison.Id,
            JobAName = comparison.JobAName,
            JobBName = comparison.JobBName,
            IsCompleted = comparison.IsCompleted,
            CreatedAt = comparison.CreatedAt,
            ExcelResultUrl = comparison.ExcelResultUrl,
            UserId = comparison.UserId,
            Answers = comparison.Answers.Select(a => new JobComparisonAnswerDto
            {
                CriterionId = a.CriterionId,
                Weight = a.Weight,
                ScoreA = a.ScoreA,
                ScoreB = a.ScoreB,
                NotApplicableA = a.NotApplicableA,
                NotApplicableB = a.NotApplicableB
            }).ToList()
        };

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobComparisons()
    {
        int userId = GetUserId();

        var list = await _jobComparisonRepo.GetAllJobComparisonsAsync(userId);

        var result = list.Select(j => new JobComparisonDto
        {
            Id = j.Id,
            JobAName = j.JobAName,
            JobBName = j.JobBName,
            IsCompleted = j.IsCompleted,
            CreatedAt = j.CreatedAt
        });

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJobComparison([FromRoute] int id)
    {
        int userId = GetUserId();

        bool deleted = await _jobComparisonRepo.DeleteJobComparisonAsync(userId, id);

        if (!deleted)
            return NotFound(new { message = "Job comparison not found." });

        return Ok(new { message = "Job comparison deleted successfully." });
    }

    [HttpGet("check")]
    public async Task<IActionResult> CheckJobComparison()
    {
        int userId = GetUserId();

        var result = await _jobComparisonRepo.GetLatestIncompleteJobComparisonAsync(userId);

        if (result == null)
            return Ok(null);

        return Ok(result);
    }

    [HttpGet("{id}/export-excel")]
    public async Task<IActionResult> ExportExcel(int id)
    {
        int userId = GetUserId();

        var result = await _excelService.GenerateJobComparisonExcelAsync(userId, id);

        Response.Headers.Append("X-Excel-Url", result.ExcelUrl);

        return File(
            result.Bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Job Comparison Scorecard.xlsx");
    }

    [HttpGet("GetAllJobComparisonsByUserId")]
    public async Task<IActionResult> GetAllJobComparisonsByUserId(int userId)
    {
        var result = await _jobComparisonRepo.GetAllJobComparisonsByUserId(userId);

        if (result == null || !result.Any())
            return NotFound(new { message = "No job comparisons found for this user." });

        return Ok(result);
    }
}
