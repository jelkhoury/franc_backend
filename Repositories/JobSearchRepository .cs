using FrancProject.Data;
using FrancProject.Dto.FrancProject.DTOs;
using FrancProject.DTOs;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Repositories
{
    public class JobSearchRepository : IJobSearchRepository
    {
        private readonly DataContext _context;

        public JobSearchRepository(DataContext context)
        {
            _context = context;
        }

        // =========================
        // CHECK SEARCH
        // =========================
        public async Task<List<JobPostResponseDto>> CheckSearchAsync(string searchKey)
        {
            var now = DateTimeOffset.UtcNow;

            var search = await _context.JobSearchCaches
                .Include(s => s.Results)
                .ThenInclude(r => r.JobPost)
                .FirstOrDefaultAsync(s => s.SearchKey == searchKey);

            if (search == null)
                return new List<JobPostResponseDto>();

            if (search.ExpiresAt <= now)
            {
                _context.JobSearchCaches.Remove(search);
                await _context.SaveChangesAsync();
                return new List<JobPostResponseDto>();
            }

            return search.Results
                .OrderBy(r => r.Position)
                .Select(r => new JobPostResponseDto
                {
                    LinkedinUrl = r.JobPost.LinkedinUrl,
                    SourceJobId = r.JobPost.SourceJobId,
                    Title = r.JobPost.Title,
                    CompanyName = r.JobPost.CompanyName,
                    LocationText = r.JobPost.LocationText,
                    DatePosted = r.JobPost.DatePosted,
                    PostedTimeAgo = r.JobPost.PostedTimeAgo,
                    IsRemote = r.JobPost.IsRemote,
                    Description = r.JobPost.Description,
                    DetailsFetchedAt = r.JobPost.DetailsFetchedAt
                })
                .ToList();
        }


        // =========================
        // STORE SEARCH + JOBS
        // =========================
        public async Task StoreSearchWithJobsAsync(
            string searchKey,
            string queryText,
            string locationUsed,
            List<JobPostDto> jobs)
        {
            var now = DateTimeOffset.UtcNow;

            var search = new JobSearchCache
            {
                SearchKey = searchKey,
                QueryText = queryText,
                LocationUsed = locationUsed,
                CreatedAt = now,
                ExpiresAt = now.AddHours(48)
            };

            int position = 0;

            foreach (var dto in jobs)
            {
                var existingJob = await _context.JobPosts
                    .FirstOrDefaultAsync(j => j.LinkedinUrl == dto.LinkedinUrl);

                if (existingJob != null)
                {
                    existingJob.LastSeenAt = now;

                    search.Results.Add(new JobSearchResult
                    {
                        JobPostId = existingJob.Id,
                        Position = position++
                    });
                }
                else
                {
                    var newJob = new JobPost
                    {
                        LinkedinUrl = dto.LinkedinUrl,
                        SourceJobId = dto.SourceJobId,
                        Title = dto.Title,
                        CompanyName = dto.CompanyName,
                        LocationText = dto.LocationText,
                        DatePosted = dto.DatePosted,
                        PostedTimeAgo = dto.PostedTimeAgo,
                        IsRemote = dto.IsRemote,
                        Description = dto.Description,
                        DetailsFetchedAt = dto.DetailsFetchedAt,
                        CreatedAt = now,
                        LastSeenAt = now
                    };

                    search.Results.Add(new JobSearchResult
                    {
                        JobPost = newJob,
                        Position = position++
                    });
                }
            }


            _context.JobSearchCaches.Add(search);
            await _context.SaveChangesAsync();
        }
    }
}
