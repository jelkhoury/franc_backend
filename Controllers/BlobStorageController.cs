using FrancProject.Dto;
using FrancProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers;

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

    [HttpPost("upload-mock-interview")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMockInterview([FromForm] MockInterviewUploadDto dto)
    {
        var result = await _blob.UploadVideosWithUserPrefixAsync(dto);
        return Ok(new { mockInterviewId = result.MockInterviewId, videoUrls = result.VideoUrls });
    }

    [HttpGet("get-all-grouped")]
    public async Task<IActionResult> GetAllVideosGrouped()
    {
        var data = await _blob.GetAllVideosGroupedByUserAsync();
        return Ok(data);
    }

    [HttpPost("create-questions")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateQuestions([FromForm] CreateQuestionDto model)
    {
        var questions = await _blob.CreateQuestionsForMajorAsync(model.MajorName, model.QuestionVideos);
        return Ok(questions);
    }

    [HttpPost("create-question")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateQuestion([FromForm] CreateSingleQuestionDto model)
    {
        var question = await _blob.CreateQuestionForMajorAsync(model.MajorName, model.Title, model.Video);
        return Ok(question);
    }

    [HttpGet("get-random-questions")]
    public async Task<IActionResult> GetRandomQuestions([FromQuery] string majorName, [FromQuery] int count = 5)
    {
        var questions = await _blob.GetRandomQuestionsByMajorAsync(majorName, count);
        return Ok(questions);
    }

    [HttpPost("create-major")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateMajor([FromForm] CreateMajorFormDto model)
    {
        var major = await _blob.CreateMajorAsync(model.Name, model.Description, model.Image, model.FacultyId);
        return Ok(major);
    }

    [HttpGet("get-majors")]
    public async Task<IActionResult> GetMajors()
    {
        var majors = await _blob.GetMajorsAsync();
        return Ok(majors);
    }

    [HttpGet("get-faculties")]
    public async Task<IActionResult> GetFaculties()
    {
        var faculties = await _blob.GetFacultiesAsync();
        return Ok(faculties);
    }

    [HttpDelete("delete-question/{id}")]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        await _blob.DeleteQuestionAsync(id);
        return Ok(new { message = "Question deleted successfully." });
    }

    [HttpPut("edit-question-title/{id}")]
    public async Task<IActionResult> EditQuestionTitle(int id, [FromQuery] string newTitle)
    {
        return Ok(new
        {
            message = "Question title updated successfully.",
            question = await _blob.UpdateQuestionTitleAsync(id, newTitle)
        });
    }

    [HttpPost("upload-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile([FromForm] UploadFileRequestDto model)
    {
        var record = await _blob.UploadFileAsync(
            model.UserId,
            model.Title,
            model.ResumeFile,
            model.CoverFile,
            model.JobAddFile,
            model.FolderName,
            model.AiEvaluation);

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

    [HttpGet("get-admin-files")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminFiles()
    {
        var files = await _blob.GetAllFilesForAdminAsync();
        return Ok(files);
    }

    [HttpGet("GetUserFiles")]
    public async Task<IActionResult> GetUserFiles(int userId)
    {
        var files = await _blob.GetFilesByUserId(userId);
        return Ok(files);
    }
}
