using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrancProject.Dto;
using FrancProject.Models;

[ApiController]
[Route("api/[controller]")]
public class BlobStorageController : ControllerBase
{
    private readonly BlobStorageService _blobStorageService;

    public BlobStorageController(BlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    [HttpPost("upload-mock-interview")]
    public async Task<IActionResult> UploadMockInterview([FromForm] MockInterviewUploadDto dto)
    {
        try
        {
           

            var (videoUrls, mockInterviewId) = await _blobStorageService.UploadVideosWithUserPrefixAsync(dto);
            return Ok(new { MockInterviewId = mockInterviewId, VideoUrls = videoUrls });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading mock interview videos: {ex.Message}");
        }
    }



    [HttpGet("all-grouped")]
    public async Task<IActionResult> GetAllVideosGrouped()
    {
        try
        {
            var groupedVideos = await _blobStorageService.GetAllVideosGroupedByUserAsync();
            return Ok(groupedVideos);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Error retrieving videos: {ex.Message}");
        }
    }

    [HttpPost("create-questions")]
    public async Task<IActionResult> CreateQuestions([FromForm] CreateQuestionDto model)
    {
        try
        {
            var questions = await _blobStorageService.CreateQuestionsForMajorAsync(model.MajorName, model.QuestionVideos);
            return Ok(questions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating questions: {ex.Message}");
        }
    }


    [HttpGet("random-questions")]
    public async Task<IActionResult> GetRandomQuestions([FromQuery] string majorName, [FromQuery] int count = 5)
    {
        try
        {
            var questions = await _blobStorageService.GetRandomQuestionsByMajorAsync(majorName, count);
            return Ok(questions);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Error retrieving questions: {ex.Message}");
        }
    }

    [HttpPost("create-major")]
    public async Task<ActionResult<Major>> CreateMajor(
        [FromForm] string name,
        [FromForm] string description,
        [FromForm] IFormFile imageFile,
        [FromForm] int facultyId)
    {
        var major = await _blobStorageService.CreateMajorAsync(name, description, imageFile, facultyId);
        return Ok(major);
    }

    [HttpPost("create-question")]
    public async Task<IActionResult> CreateQuestion([FromForm] string majorName, [FromForm] string title, [FromForm] IFormFile questionVideo)
    {
        try
        {
            var question = await _blobStorageService.CreateQuestionForMajorAsync(majorName, title, questionVideo);
            return Ok(question);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating question: {ex.Message}");
        }
    }

    [HttpGet("get-majors")]
    public async Task<IActionResult> GetMajors()
    {
        try
        {
            var majors = await _blobStorageService.GetMajorsAsync();
            return Ok(majors);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving majors: {ex.Message}");
        }
    }

    [HttpGet("get-faculties")]
    public async Task<ActionResult<List<Faculty>>> GetFaculties()
    {
        try
        {
            var faculties = await _blobStorageService.GetFacultiesAsync();
            return Ok(faculties);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving faculties: {ex.Message}");
        }
    }


}
