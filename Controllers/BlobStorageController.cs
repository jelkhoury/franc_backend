using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrancProject.Dto;
using FrancProject.Models;
using System;

[ApiController]
[Route("api/blob")]
[Authorize]
public class BlobStorageController : ControllerBase
{
    private readonly BlobStorageService _blob;

    public BlobStorageController(BlobStorageService blobStorageService)
    {
        _blob = blobStorageService;
    }

    // ---------------------------------------
    // UPLOAD MOCK INTERVIEW
    // ---------------------------------------
    [HttpPost("upload-mock-interview")]
    public async Task<IActionResult> UploadMockInterview([FromForm] MockInterviewUploadDto dto)
    {
        try
        {
            var result = await _blob.UploadVideosWithUserPrefixAsync(dto);
            return Ok(new { mockInterviewId = result.MockInterviewId, videoUrls = result.VideoUrls });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error uploading mock interview: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET ALL VIDEOS GROUPED BY USER
    // ---------------------------------------
    [HttpGet("get-all-grouped")]
    public async Task<IActionResult> GetAllVideosGrouped()
    {
        try
        {
            var data = await _blob.GetAllVideosGroupedByUserAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error retrieving videos: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // CREATE MULTIPLE QUESTIONS FOR MAJOR
    // ---------------------------------------
    [HttpPost("create-questions")]
    public async Task<IActionResult> CreateQuestions([FromForm] CreateQuestionDto model)
    {
        try
        {
            var questions = await _blob.CreateQuestionsForMajorAsync(model.MajorName, model.QuestionVideos);
            return Ok(questions);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error creating questions: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // CREATE SINGLE QUESTION
    // ---------------------------------------
    [HttpPost("create-question")]
    public async Task<IActionResult> CreateQuestion(
        [FromForm] string majorName,
        [FromForm] string title,
        [FromForm] IFormFile video)
    {
        try
        {
            var question = await _blob.CreateQuestionForMajorAsync(majorName, title, video);
            return Ok(question);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error creating question: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET RANDOM QUESTIONS FOR MAJOR
    // ---------------------------------------
    [HttpGet("get-random-questions")]
    public async Task<IActionResult> GetRandomQuestions([FromQuery] string majorName, [FromQuery] int count = 5)
    {
        try
        {
            var questions = await _blob.GetRandomQuestionsByMajorAsync(majorName, count);
            return Ok(questions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error retrieving random questions: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // CREATE MAJOR
    // ---------------------------------------
    [HttpPost("create-major")]
    public async Task<IActionResult> CreateMajor(
        [FromForm] string name,
        [FromForm] string description,
        [FromForm] IFormFile image,
        [FromForm] int facultyId)
    {
        try
        {
            var major = await _blob.CreateMajorAsync(name, description, image, facultyId);
            return Ok(major);
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
            return StatusCode(500, new { error = $"Error creating major: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET ALL MAJORS
    // ---------------------------------------
    [HttpGet("get-majors")]
    public async Task<IActionResult> GetMajors()
    {
        try
        {
            var majors = await _blob.GetMajorsAsync();
            return Ok(majors);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error retrieving majors: {ex.Message}" });
        }
    }

    // ---------------------------------------
    // GET ALL FACULTIES
    // ---------------------------------------
    [HttpGet("get-faculties")]
    public async Task<IActionResult> GetFaculties()
    {
        try
        {
            var faculties = await _blob.GetFacultiesAsync();
            return Ok(faculties);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Error retrieving faculties: {ex.Message}" });
        }
    }

    [HttpDelete("delete-question/{id}")]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        try
        {
            await _blob.DeleteQuestionAsync(id);
            return Ok(new { message = "Question deleted successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }



    [HttpPut("edit-question-title/{id}")]
    public async Task<IActionResult> EditQuestionTitle(int id, [FromQuery] string newTitle)
    {
        try
        {
            return Ok(new
            {
                message = "Question title updated successfully.",
                question = await _blob.UpdateQuestionTitleAsync(id, newTitle)
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [HttpPost("upload-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(
      [FromForm] UploadFileRequestDto model)
    {
        try
        {
            var record = await _blob.UploadFileAsync(
                model.UserId,
                model.Title,
                model.ResumeFile,
                model.CoverFile,
                model.JobAddFile,
                model.FolderName,
                model.AiEvaluation
            );

            return Ok(new
            {
                message = "File(s) uploaded successfully",
                fileId = record.Id,
                title = record.Title,
                resumeUrl = record.ResumeUrl,
                coverUrl = record.CoverUrl,
                jobAddUrl = record.JobAddUrl
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }



}