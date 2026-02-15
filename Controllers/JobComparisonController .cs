using FrancProject.Dto;
using FrancProject.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/jobcomparison")]
[Authorize]
public class JobComparisonController : ControllerBase
{
    private readonly IJobComparisonRepository _jobComparisonRepo;
    private readonly JobComparisonExcelService _excelService;

    public JobComparisonController(
        IJobComparisonRepository jobComparisonRepo,
        JobComparisonExcelService excelService)
    {
        _jobComparisonRepo = jobComparisonRepo;
        _excelService = excelService;
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    // ---------------------------------------
    // GET CRITERIA
    // ---------------------------------------
    [HttpGet("criteria")]
    public async Task<IActionResult> GetCriteria()
    {
        try
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
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("criteria/bulk")]
    [Authorize(Roles = "Admin")] // optional
    public async Task<IActionResult> BulkCreateCriteria(
    [FromBody] List<CreateJobComparisonCriterionDto> dtos)
    {
        try
        {
            if (dtos == null || dtos.Count == 0)
                return BadRequest(new { error = "Criteria list cannot be empty." });

            var created =
                await _jobComparisonRepo.BulkCreateCriteriaAsync(dtos);

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
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // SAVE JOB COMPARISON (CREATE / UPDATE)
    // ---------------------------------------
    [HttpPost("save")]
    public async Task<IActionResult> SaveJobComparison(
        [FromBody] SaveJobComparisonDto dto)
    {
        try
        {
            int userId = GetUserId();

            int jobComparisonId =
                await _jobComparisonRepo.SaveJobComparisonAsync(userId, dto);

            return Ok(new
            {
                message = "Job comparison saved successfully.",
                jobComparisonId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // GET SINGLE JOB COMPARISON
    // ---------------------------------------
    [HttpGet("{id}")]
    public async Task<IActionResult> GetJobComparison([FromRoute] int id)
    {
        try
        {
            int userId = GetUserId();

            var comparison =
                await _jobComparisonRepo.GetJobComparisonAsync(userId, id);

            if (comparison == null)
                return NotFound(new { message = "Job comparison not found." });

            var result = new JobComparisonDto
            {
                Id = comparison.Id,
                JobAName = comparison.JobAName,
                JobBName = comparison.JobBName,
                IsCompleted = comparison.IsCompleted,
                CreatedAt = comparison.CreatedAt,
                ExcelResultUrl=comparison.ExcelResultUrl,
                UserId=comparison.UserId,
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
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // GET ALL JOB COMPARISONS (USER)
    // ---------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAllJobComparisons()
    {
        try
        {
            int userId = GetUserId();

            var list =
                await _jobComparisonRepo.GetAllJobComparisonsAsync(userId);

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
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---------------------------------------
    // DELETE JOB COMPARISON
    // ---------------------------------------
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJobComparison([FromRoute] int id)
    {
        try
        {
            int userId = GetUserId();

            bool deleted =
                await _jobComparisonRepo.DeleteJobComparisonAsync(userId, id);

            if (!deleted)
                return NotFound(new { message = "Job comparison not found." });

            return Ok(new { message = "Job comparison deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("check")]
    public async Task<IActionResult> CheckJobComparison()
    {
        int userId = GetUserId(); 

        var result =
            await _jobComparisonRepo.GetLatestIncompleteJobComparisonAsync(userId);

        if (result == null)
            return Ok(null);

        return Ok(result);
    }


    [HttpGet("{id}/export-excel")]
    public async Task<IActionResult> ExportExcel(int id)
    {
        int userId = GetUserId();

        var result = await _excelService
            .GenerateJobComparisonExcelAsync(userId, id);

        // result = (byte[] bytes, string url)

        Response.Headers.Add("X-Excel-Url", result.ExcelUrl);

        return File(
            result.Bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Job Comparison Scorecard.xlsx");
    }




}

