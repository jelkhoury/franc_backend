using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using FrancProject.Data;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using FrancProject.Dto;
using FrancProject.Interface;



public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly DataContext _context;
    private readonly IUserRepository _userRepository;

    public BlobStorageService(IConfiguration configuration, DataContext context, IUserRepository userRepository)
    {
        string connectionString = configuration.GetConnectionString("AzureBlobStorage");
        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = configuration["AzureBlobStorage:ContainerName"];
        _context = context;
        _userRepository = userRepository;
    }






    public async Task<List<UserMockInterviewAnswersDto>> GetAllVideosGroupedByUserAsync()
    {
        var grouped = await _context.Answers
            .Include(a => a.User)
            .Include(a => a.Question)
            .Include(a => a.MockInterview)
            .Where(a => a.MockInterviewId != null)
            .GroupBy(a => new
            {
                a.UserId,
                a.User.Email,
                MockInterviewId = a.MockInterviewId.Value,
                MockInterviewTitle = a.MockInterview.Title,
                NbOfTry = a.MockInterview.NbOfTry   // include NbOfTry here
            })
            .Select(g => new UserMockInterviewAnswersDto
            {
                UserId = g.Key.UserId,
                Email = g.Key.Email,
                MockInterviewId = g.Key.MockInterviewId,
                MockInterviewTitle = g.Key.MockInterviewTitle,
                NbOfTry = g.Key.NbOfTry,           // assign it to the DTO
                Answers = g.Select(a => new AnswerWithQuestionDto
                {
                    AnswerId = a.Id,
                    VideoUrl = a.VideoUrl,
                    QuestionId = a.QuestionId,
                    QuestionTitle = a.Question.Title
                }).ToList()
            })
            .ToListAsync();

        return grouped;
    }




    public async Task<(List<string> VideoUrls, int MockInterviewId)> UploadVideosWithUserPrefixAsync(MockInterviewUploadDto dto)
    {
        if (dto.Videos == null || dto.Videos.Count == 0)
            throw new ArgumentException("No video files provided");

        if (dto.QuestionIds == null || dto.QuestionIds.Count != dto.Videos.Count)
            throw new ArgumentException("Question IDs must match the number of videos");

        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null)
            throw new ArgumentException("User not found");

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        var mockInterview = new MockInterview
        {
            Title = $"Mock Interview {user.Email}",
            Duration = dto.Duration,
            NbOfTry=dto.NbOfTry

        };

        _context.MockInterviews.Add(mockInterview);
        await _context.SaveChangesAsync();

        var videoUrls = new List<string>();

        for (int i = 0; i < dto.Videos.Count; i++)
        {
            var video = dto.Videos[i];
            if (video.Length > 0)
            {
                string extension = Path.GetExtension(video.FileName);
                string blobName = $"User{dto.UserId}_Mock{mockInterview.Id}_Answer{i + 1}{extension}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using (var stream = video.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                string videoUrl = blobClient.Uri.ToString();
                videoUrls.Add(videoUrl);

                var answer = new Answer
                {
                    UserId = dto.UserId,
                    QuestionId = dto.QuestionIds[i],
                    VideoUrl = videoUrl,
                    CreatedAt = DateTime.UtcNow,
                    MockInterviewId = mockInterview.Id
                };

                _context.Answers.Add(answer);
            }
        }

        user.CanDoMockInterview = false;
        _context.Users.Update(user);

        await _context.SaveChangesAsync();
        await _userRepository.SendEmailAsync(dto.UserId);

        return (videoUrls, mockInterview.Id);
    }




    public async Task<List<Question>> CreateQuestionsForMajorAsync(string majorName, List<IFormFile> questionVideos)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name is required");

        if (questionVideos == null || questionVideos.Count == 0)
            throw new ArgumentException("At least one question video must be provided");

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();
        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Name == majorName);
        if (major == null)
        {
            major = new Major { Name = majorName };
            _context.Majors.Add(major);
            await _context.SaveChangesAsync();
        }

        var createdQuestions = new List<Question>();

        for (int i = 0; i < questionVideos.Count; i++)
        {
            var video = questionVideos[i];
            if (video.Length > 0)
            {
                string extension = Path.GetExtension(video.FileName);
                string blobName = $"{majorName}_Question{i + 1}{extension}";
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                using (var stream = video.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                string videoUrl = blobClient.Uri.ToString();

                var question = new Question
                {
                    Title = $"Question {i + 1} for {majorName}",
                    VideoUrl = videoUrl,
                    MajorId = major.Id
                };

                _context.Questions.Add(question);
                createdQuestions.Add(question);
            }
        }

        await _context.SaveChangesAsync();

        return createdQuestions;
    }


    public async Task<Question> CreateQuestionForMajorAsync(string majorName, string title, IFormFile questionVideo)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name is required");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Question title is required");

        if (questionVideo == null || questionVideo.Length == 0)
            throw new ArgumentException("A question video must be provided");

        // Ensure container exists
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        // Ensure major exists
        var major = await _context.Majors.FirstOrDefaultAsync(m => m.Name == majorName);
        if (major == null)
        {
            major = new Major { Name = majorName };
            _context.Majors.Add(major);
            await _context.SaveChangesAsync();
        }

        // Sanitize title for blob name (remove invalid characters)
        string safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        string extension = Path.GetExtension(questionVideo.FileName);
        string blobName = $"{safeTitle}{extension}";

        // Upload video to Azure Blob Storage
        BlobClient blobClient = containerClient.GetBlobClient(blobName);
        using (var stream = questionVideo.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        string videoUrl = blobClient.Uri.ToString();

        // Create and save question
        var question = new Question
        {
            Title = title,
            VideoUrl = videoUrl,
            MajorId = major.Id
        };

        _context.Questions.Add(question);
        await _context.SaveChangesAsync();

        return question;
    }



    public async Task<List<QuestionWithMajorDto>> GetRandomQuestionsByMajorAsync(
     string majorName,
     int randomCount = 5)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name is required", nameof(majorName));

        var major = await _context.Majors
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Name == majorName);

        if (major == null)
            return new List<QuestionWithMajorDto>();

        // 1) Fixed questions (always included, regardless of major)
        var fixedTitles = new[]
        {
        "Opening Message",
        "Closing Message",
        "Candidate Question"
    };

        var fixedTitlesLower = fixedTitles.Select(t => t.ToLower()).ToArray();

        var fixedQuestions = await _context.Questions
            .AsNoTracking()
            .Where(q => fixedTitlesLower.Contains(q.Title.ToLower()))
            .Select(q => new QuestionWithMajorDto
            {
                QuestionId = q.Id,
                Title = q.Title,
                VideoUrl = q.VideoUrl,
                MajorId = q.MajorId,
                MajorName = q.MajorId != null
                    ? _context.Majors.FirstOrDefault(m => m.Id == q.MajorId)!.Name
                    : null
            })
            .ToListAsync();

        var fixedIds = fixedQuestions.Select(q => q.QuestionId).ToHashSet();

        // 2) Random questions for this specific major
        var randomQuestions = await _context.Questions
            .AsNoTracking()
            .Where(q => q.MajorId == major.Id && !fixedIds.Contains(q.Id))
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

        // 3) Combine results: 3 fixed + 5 random
        return fixedQuestions.Concat(randomQuestions).ToList();
    }
    public async Task<Major> CreateMajorAsync(string name, string description, IFormFile imageFile, int facultyId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Major name is required");

        if (imageFile == null || imageFile.Length == 0)
            throw new ArgumentException("Image file is required");

        var existingMajor = await _context.Majors.FirstOrDefaultAsync(m => m.Name == name);
        if (existingMajor != null)
            throw new InvalidOperationException("A major with the same name already exists.");

        var faculty = await _context.Faculties.FindAsync(facultyId);
        if (faculty == null)
            throw new ArgumentException("Faculty not found");

 
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        string extension = Path.GetExtension(imageFile.FileName);
        string blobName = $"{Guid.NewGuid()}{extension}";
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using (var stream = imageFile.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        string imageUrl = blobClient.Uri.ToString();

        var major = new Major
        {
            Name = name,
            Description = description,
            UrlImage = imageUrl, 
            FacultyId = facultyId
        };

        _context.Majors.Add(major);
        await _context.SaveChangesAsync();

        return major;
    }



    public async Task<List<MajorDto>> GetMajorsAsync()
    {
        return await _context.Majors
            .Select(m => new MajorDto
            {
                Id = m.Id,
                Name = m.Name,
                UrlImage = m.UrlImage,
                Description = m.Description,
                FacultyId = m.FacultyId
            })
            .ToListAsync();
    }

    public async Task<List<Faculty>> GetFacultiesAsync()
    {
        return await _context.Faculties.ToListAsync();
    }








}