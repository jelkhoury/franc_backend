using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interface;
using FrancProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class BlobStorageService
{
    private readonly BlobServiceClient _blobClient;
    private readonly string _container;
    private readonly DataContext _context;
    private readonly IUserRepository _userRepo;

    public BlobStorageService(IConfiguration config, DataContext context, IUserRepository userRepository)
    {
        _blobClient = new BlobServiceClient(config.GetConnectionString("AzureBlobStorage"));
        _container = config["AzureBlobStorage:ContainerName"];

        _context = context;
        _userRepo = userRepository;
    }

    // ---------------------------------------
    // GROUP VIDEOS BY USER
    // ---------------------------------------
    public async Task<List<UserMockInterviewAnswersDto>> GetAllVideosGroupedByUserAsync()
    {
        var data = await _context.Answers
            .AsNoTracking()
            .Where(a => a.MockInterviewId != null && !a.MockInterview.IsEvaluated)
            .Select(a => new
            {
                AnswerId = a.Id,
                a.VideoUrl,
                a.UserId,
                Email = a.User.Email,
                MockInterviewId = a.MockInterviewId,
                MockInterviewTitle = a.MockInterview.Title,
                NbOfTry = a.MockInterview.NbOfTry,
                IsEvaluated = a.MockInterview.IsEvaluated,
                a.QuestionId,
                QuestionTitle = a.Question.Title
            })
            .ToListAsync();

        var result = data
            .GroupBy(a => new
            {
                a.UserId,
                a.Email,
                MockInterviewId = a.MockInterviewId!.Value,
                a.MockInterviewTitle,
                a.NbOfTry,
                a.IsEvaluated
            })
            .Select(g => new UserMockInterviewAnswersDto
            {
                UserId = g.Key.UserId,
                Email = g.Key.Email,
                MockInterviewId = g.Key.MockInterviewId,
                MockInterviewTitle = g.Key.MockInterviewTitle,
                NbOfTry = g.Key.NbOfTry,
                IsEvaluated = g.Key.IsEvaluated,
                Answers = g.Select(a => new AnswerWithQuestionDto
                {
                    AnswerId = a.AnswerId,
                    VideoUrl = a.VideoUrl,
                    QuestionId = a.QuestionId,
                    QuestionTitle = a.QuestionTitle
                }).ToList()
            })
            .ToList();

        return result;
    }

    // ---------------------------------------
    // UPLOAD MOCK INTERVIEW
    // ---------------------------------------
    public async Task<(List<string> VideoUrls, int MockInterviewId)> UploadVideosWithUserPrefixAsync(
        MockInterviewUploadDto dto)
    {
        if (dto.Videos == null || dto.Videos.Count == 0)
            throw new ArgumentException("No video files provided.");

        if (dto.QuestionIds == null || dto.QuestionIds.Count != dto.Videos.Count)
            throw new ArgumentException("QuestionIds count must match videos count.");

        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null)
            throw new ArgumentException("User not found.");

        var containerClient = _blobClient.GetBlobContainerClient(_container);
        await containerClient.CreateIfNotExistsAsync();

        var mock = new MockInterview
        {
            Title = $"Mock Interview {user.Email}",
            Duration = dto.Duration,
            NbOfTry = dto.NbOfTry
        };

        _context.MockInterviews.Add(mock);
        await _context.SaveChangesAsync();

        var urls = new List<string>();

        for (int i = 0; i < dto.Videos.Count; i++)
        {
            var file = dto.Videos[i];
            string ext = Path.GetExtension(file.FileName);
            string blobName = $"User{dto.UserId}_Mock{mock.Id}_Answer{i + 1}{ext}";

            using var stream = file.OpenReadStream();
            var blob = containerClient.GetBlobClient(blobName);

            await blob.UploadAsync(stream, overwrite: true);

            string videoUrl = blob.Uri.ToString();
            urls.Add(videoUrl);

            var answer = new Answer
            {
                UserId = dto.UserId,
                QuestionId = dto.QuestionIds[i],
                VideoUrl = videoUrl,
                MockInterviewId = mock.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Answers.Add(answer);
        }

        user.CanDoMockInterview = false;
        _context.Users.Update(user);

        await _context.SaveChangesAsync();

        await _userRepo.SendEmailAsync(dto.UserId);

        return (urls, mock.Id);
    }

    // ---------------------------------------
    // CREATE MULTIPLE QUESTIONS
    // ---------------------------------------
    public async Task<List<Question>> CreateQuestionsForMajorAsync(string majorName, List<IFormFile> videos)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name required.");

        if (videos == null || videos.Count == 0)
            throw new ArgumentException("No videos uploaded.");

        var container = _blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Name == majorName);
        if (major == null)
        {
            major = new Major { Name = majorName };
            _context.Majors.Add(major);
            await _context.SaveChangesAsync();
        }

        var list = new List<Question>();

        for (int i = 0; i < videos.Count; i++)
        {
            var video = videos[i];
            string ext = Path.GetExtension(video.FileName);
            string blobName = $"{majorName}_Question{i + 1}{ext}";
            var blob = container.GetBlobClient(blobName);

            using var stream = video.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            var q = new Question
            {
                Title = $"Question {i + 1} for {majorName}",
                VideoUrl = blob.Uri.ToString(),
                MajorId = major.Id
            };

            list.Add(q);
            _context.Questions.Add(q);
        }

        await _context.SaveChangesAsync();

        return list;
    }

    // ---------------------------------------
    // CREATE SINGLE QUESTION
    // ---------------------------------------
    public async Task<Question> CreateQuestionForMajorAsync(string majorName, string title, IFormFile video)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name required.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");

        if (video == null || video.Length == 0)
            throw new ArgumentException("Video required.");

        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Name == majorName);
        if (major == null)
        {
            major = new Major { Name = majorName };
            _context.Majors.Add(major);
            await _context.SaveChangesAsync();
        }

        var container = _blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        // Sanitize title for filename
        string safe = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        string ext = Path.GetExtension(video.FileName);

        // UNIQUE name
        string uniqueId = Guid.NewGuid().ToString();
        string blobName = $"{safe}_{uniqueId}{ext}";

        var blob = container.GetBlobClient(blobName);

        using var stream = video.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true);

        var q = new Question
        {
            Title = title,
            VideoUrl = blob.Uri.ToString(),
            MajorId = major.Id
        };

        _context.Questions.Add(q);
        await _context.SaveChangesAsync();

        return q;
    }


    // ---------------------------------------
    // RANDOM QUESTIONS BY MAJOR
    // ---------------------------------------
    public async Task<List<QuestionWithMajorDto>> GetRandomQuestionsByMajorAsync(string majorName, int randomCount = 5)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name is required.");

        var major = await _context.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Name == majorName);
        if (major == null)
            return new List<QuestionWithMajorDto>();

        // fixed questions
        var fixedTitles = new[] { "Opening Message", "Candidate Question", "Closing Message" };
        var lower = fixedTitles.Select(t => t.ToLower()).ToArray();

        var fixedQuestions = await _context.Questions
            .AsNoTracking()
            .Where(q => lower.Contains(q.Title.ToLower()))
            .Select(q => new QuestionWithMajorDto
            {
                QuestionId = q.Id,
                Title = q.Title,
                VideoUrl = q.VideoUrl,
                MajorId = q.MajorId,
                MajorName = q.MajorId != null ? _context.Majors.First(m => m.Id == q.MajorId).Name : null
            })
            .ToListAsync();

        var fixedIds = fixedQuestions.Select(f => f.QuestionId).ToHashSet();

        // common questions
        var common = await _context.Questions
            .AsNoTracking()
            .Where(q => q.Title.ToLower() == "common question")
            .Select(q => new QuestionWithMajorDto
            {
                QuestionId = q.Id,
                Title = q.Title,
                VideoUrl = q.VideoUrl,
                MajorId = q.MajorId,
                MajorName = null
            })
            .ToListAsync();

        var excluded = fixedIds.Union(common.Select(c => c.QuestionId)).ToHashSet();

        // random questions
        var random = await _context.Questions
            .AsNoTracking()
            .Where(q => q.MajorId == major.Id && !excluded.Contains(q.Id))
            .OrderBy(q => Guid.NewGuid())
            .Take(randomCount)
            .Select(q => new QuestionWithMajorDto
            {
                QuestionId = q.Id,
                Title = q.Title,
                VideoUrl = q.VideoUrl,
                MajorId = q.MajorId,
                MajorName = major.Name
            })
            .ToListAsync();

        return fixedQuestions.Concat(common).Concat(random).ToList();
    }

    // ---------------------------------------
    // CREATE MAJOR
    // ---------------------------------------
    public async Task<Major> CreateMajorAsync(string name, string description, IFormFile image, int facultyId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Major name required.");

        if (image == null || image.Length == 0)
            throw new ArgumentException("Image file required.");

        var exists = await _context.Majors.AnyAsync(m => m.Name == name);
        if (exists)
            throw new InvalidOperationException("Major already exists.");

        var faculty = await _context.Faculties.FindAsync(facultyId);
        if (faculty == null)
            throw new ArgumentException("Faculty not found.");

        var container = _blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        string ext = Path.GetExtension(image.FileName);
        string blobName = $"{Guid.NewGuid()}{ext}";

        var blob = container.GetBlobClient(blobName);
        using var stream = image.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true);

        var major = new Major
        {
            Name = name,
            Description = description,
            UrlImage = blob.Uri.ToString(),
            FacultyId = facultyId
        };

        _context.Majors.Add(major);
        await _context.SaveChangesAsync();

        return major;
    }

    // ---------------------------------------
    // GET MAJORS
    // ---------------------------------------
    public async Task<List<MajorDto>> GetMajorsAsync()
    {
        return await _context.Majors
            .Select(m => new MajorDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                UrlImage = m.UrlImage,
                FacultyId = m.FacultyId
            })
            .ToListAsync();
    }

    // ---------------------------------------
    // GET FACULTIES
    // ---------------------------------------
    public async Task<List<Faculty>> GetFacultiesAsync()
    {
        return await _context.Faculties.ToListAsync();
    }


    public async Task<bool> DeleteQuestionAsync(int questionId)
    {
        var question = await _context.Questions
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            return false;

        _context.Questions.Remove(question);
        await _context.SaveChangesAsync();

        return true;
    }
    public async Task<Question> UpdateQuestionTitleAsync(int questionId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("New title required.");

        var question = await _context.Questions
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            throw new Exception("Question not found.");

        question.Title = newTitle.Trim();

        await _context.SaveChangesAsync();

        return question;
    }

    public async Task<FileRecord> UploadFileAsync(
      int userId,
      string title,
      IFormFile? resumeFile,
      IFormFile? coverFile,
      IFormFile? jobAddFile,
      string folderName,
      string? aiEvaluation = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found.");

        var container = _blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        async Task<string?> UploadToBlobAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            string ext = Path.GetExtension(file.FileName);
            string baseName = Path.GetFileNameWithoutExtension(file.FileName);
            baseName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars()));

            string unique =
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" +
                Guid.NewGuid().ToString("N");

            string blobName = $"{baseName}_{unique}{ext}";
            string blobPath = $"{folderName}/{blobName}";

            var blob = container.GetBlobClient(blobPath);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: false);

            return blob.Uri.ToString();
        }

        var record = new FileRecord
        {
            UserId = userId,
            Title = title,
            AiEvaluation = aiEvaluation
        };

        switch (title)
        {
            case "Resume":
                if ((user.ResumeAttempts ?? 0) <= 0)
                    throw new InvalidOperationException("No resume attempts remaining.");

                record.ResumeUrl = await UploadToBlobAsync(resumeFile);
                user.ResumeAttempts--;
                break;

            case "CoverLetter":
                if ((user.CoverAttempts ?? 0) <= 0)
                    throw new InvalidOperationException("No cover letter attempts remaining.");

                if (coverFile == null || jobAddFile == null)
                    throw new ArgumentException("CoverLetter requires cover file and job ad file.");

                record.CoverUrl = await UploadToBlobAsync(coverFile);
                record.JobAddUrl = await UploadToBlobAsync(jobAddFile);
                user.CoverAttempts--;
                break;

            case "JobAdd":
                record.JobAddUrl = await UploadToBlobAsync(jobAddFile);
                break;

            default:
                throw new ArgumentException("Invalid title. Allowed: Resume, CoverLetter, JobAdd.");
        }

        _context.Files.Add(record);
        _context.Users.Update(user); // 🔑 ensure EF tracks the decrement
        await _context.SaveChangesAsync();

        return record;
    }


    public async Task<List<AdminFileDto>> GetAllFilesForAdminAsync()
    {
        var result = await _context.Files
            .Include(f => f.User)
            .Select(f => new AdminFileDto
            {
                Id = f.Id,
                Title = f.Title,
                ResumeUrl = f.ResumeUrl,
                CoverUrl = f.CoverUrl,
                JobAddUrl = f.JobAddUrl,
                AiEvaluation = f.AiEvaluation,
                UserEmail = f.User.Email
            })
            .ToListAsync();

        return result;
    }

    public async Task<List<AdminFileDto>> GetFilesByUserId(int userId)
    {
        return await _context.Files
            .Where(f => f.UserId == userId)
            .Select(f => new AdminFileDto
            {
                Id = f.Id,
                Title = f.Title,
                ResumeUrl = f.ResumeUrl,
                CoverUrl = f.CoverUrl,
                JobAddUrl = f.JobAddUrl,
                AiEvaluation = f.AiEvaluation,
                UserEmail = f.User.Email
            })
            .ToListAsync();
    }

    public async Task<string> UploadJobComparisonExcelAsync(
      int jobComparisonId,
      byte[] excelBytes)
    {
        if (excelBytes == null || excelBytes.Length == 0)
            throw new ArgumentException("Excel content is empty.");

        var container = _blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        string blobName =
            $"job-comparisons/JobComparison_{jobComparisonId}_{Guid.NewGuid():N}.xlsx";

        var blob = container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType =
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ContentDisposition =
                "attachment; filename=\"Job Comparison Scorecard.xlsx\"",
            CacheControl = "no-cache, no-store, must-revalidate"
        };

        // 🔥 IMPORTANT: upload bytes directly, not via stream
        await blob.UploadAsync(
            BinaryData.FromBytes(excelBytes),
            new BlobUploadOptions { HttpHeaders = headers });

        return blob.Uri.ToString();
    }






}


