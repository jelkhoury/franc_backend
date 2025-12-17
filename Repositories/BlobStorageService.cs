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

    public BlobStorageService(
        IConfiguration configuration,
        DataContext context,
        IUserRepository userRepository)
    {
        // -------------------------------
        // BLOB CONNECTION STRING
        // -------------------------------
        var blobConnectionString =
            configuration.GetConnectionString("AzureBlobStorage");

        if (string.IsNullOrWhiteSpace(blobConnectionString))
            throw new InvalidOperationException(
                "AzureBlobStorage connection string is missing");

        _blobServiceClient = new BlobServiceClient(blobConnectionString);

        // -------------------------------
        // CONTAINER NAME (FLAT KEY)
        // -------------------------------
        _containerName = configuration["BlobContainerName"];

        if (string.IsNullOrWhiteSpace(_containerName))
            throw new InvalidOperationException(
                "BlobContainerName is missing");

        _context = context;
        _userRepository = userRepository;
    }

    // --------------------------------------------------
    // GROUP VIDEOS BY USER
    // --------------------------------------------------
    public async Task<List<UserMockInterviewAnswersDto>> GetAllVideosGroupedByUserAsync()
    {
        return await _context.Answers
            .Include(a => a.User)
            .Include(a => a.Question)
            .Include(a => a.MockInterview)
            .Where(a => a.MockInterviewId != null)
            .GroupBy(a => new
            {
                a.UserId,
                a.User.Email,
                MockInterviewId = a.MockInterviewId!.Value,
                MockInterviewTitle = a.MockInterview.Title,
                NbOfTry = a.MockInterview.NbOfTry
            })
            .Select(g => new UserMockInterviewAnswersDto
            {
                UserId = g.Key.UserId,
                Email = g.Key.Email,
                MockInterviewId = g.Key.MockInterviewId,
                MockInterviewTitle = g.Key.MockInterviewTitle,
                NbOfTry = g.Key.NbOfTry,
                Answers = g.Select(a => new AnswerWithQuestionDto
                {
                    AnswerId = a.Id,
                    VideoUrl = a.VideoUrl,
                    QuestionId = a.QuestionId,
                    QuestionTitle = a.Question.Title
                }).ToList()
            })
            .ToListAsync();
    }

    // --------------------------------------------------
    // UPLOAD MOCK INTERVIEW VIDEOS
    // --------------------------------------------------
    public async Task<(List<string> VideoUrls, int MockInterviewId)>
        UploadVideosWithUserPrefixAsync(MockInterviewUploadDto dto)
    {
        if (dto.Videos == null || dto.Videos.Count == 0)
            throw new ArgumentException("No video files provided");

        if (dto.QuestionIds == null || dto.QuestionIds.Count != dto.Videos.Count)
            throw new ArgumentException("Question IDs must match video count");

        var user = await _context.Users.FindAsync(dto.UserId)
            ?? throw new ArgumentException("User not found");

        var containerClient =
            _blobServiceClient.GetBlobContainerClient(_containerName);

        await containerClient.CreateIfNotExistsAsync();

        var mockInterview = new MockInterview
        {
            Title = $"Mock Interview {user.Email}",
            Duration = dto.Duration,
            NbOfTry = dto.NbOfTry
        };

        _context.MockInterviews.Add(mockInterview);
        await _context.SaveChangesAsync();

        var urls = new List<string>();

        for (int i = 0; i < dto.Videos.Count; i++)
        {
            var video = dto.Videos[i];
            string ext = Path.GetExtension(video.FileName);
            string blobName =
                $"User{dto.UserId}_Mock{mockInterview.Id}_Answer{i + 1}{ext}";

            var blob = containerClient.GetBlobClient(blobName);

            using var stream = video.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            string url = blob.Uri.ToString();
            urls.Add(url);

            _context.Answers.Add(new Answer
            {
                UserId = dto.UserId,
                QuestionId = dto.QuestionIds[i],
                VideoUrl = url,
                CreatedAt = DateTime.UtcNow,
                MockInterviewId = mockInterview.Id
            });
        }

        user.CanDoMockInterview = false;
        _context.Users.Update(user);

        await _context.SaveChangesAsync();
        await _userRepository.SendEmailAsync(dto.UserId);

        return (urls, mockInterview.Id);
    }

    // --------------------------------------------------
    // CREATE QUESTIONS FOR MAJOR (MULTIPLE)
    // --------------------------------------------------
    public async Task<List<Question>> CreateQuestionsForMajorAsync(
        string majorName,
        List<IFormFile> videos)
    {
        if (string.IsNullOrWhiteSpace(majorName))
            throw new ArgumentException("Major name required");

        var container =
            _blobServiceClient.GetBlobContainerClient(_containerName);

        await container.CreateIfNotExistsAsync();

        var major = await _context.Majors
            .FirstOrDefaultAsync(m => m.Name == majorName)
            ?? new Major { Name = majorName };

        if (major.Id == 0)
        {
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

            _context.Questions.Add(q);
            list.Add(q);
        }

        await _context.SaveChangesAsync();
        return list;
    }

    // --------------------------------------------------
    // GET MAJORS
    // --------------------------------------------------
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

    // --------------------------------------------------
    // GET FACULTIES
    // --------------------------------------------------
    public async Task<List<Faculty>> GetFacultiesAsync()
    {
        return await _context.Faculties.ToListAsync();
    }
}
