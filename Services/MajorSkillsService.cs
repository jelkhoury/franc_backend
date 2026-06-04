using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Services
{
    public class MajorSkillsService : IMajorSkillsService
    {
        private readonly DataContext _context;

        public MajorSkillsService(DataContext context)
        {
            _context = context;
        }

        public async Task<MajorSkillsCacheDto?> CheckAsync(string searchKey)
        {
            var entity = await _context.MajorSkillsCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SearchKey == searchKey);

            if (entity == null)
                return null;

            return MapToDto(entity);
        }

        public async Task StoreAsync(StoreMajorSkillsRequestDto request)
        {
            var now = DateTimeOffset.UtcNow;

            var existing = await _context.MajorSkillsCaches
                .FirstOrDefaultAsync(x => x.SearchKey == request.SearchKey);

            if (existing == null)
            {
                _context.MajorSkillsCaches.Add(new MajorSkillsCache
                {
                    SearchKey = request.SearchKey,
                    Faculty = request.Faculty,
                    Major = request.Major,
                    Level = request.Level,
                    Country = request.Country,
                    ResultJson = request.ResultJson,
                    PromptVersion = request.PromptVersion,
                    ModelName = request.ModelName,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.Faculty = request.Faculty;
                existing.Major = request.Major;
                existing.Level = request.Level;
                existing.Country = request.Country;
                existing.ResultJson = request.ResultJson;
                existing.PromptVersion = request.PromptVersion;
                existing.ModelName = request.ModelName;
                existing.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
        }

        private static MajorSkillsCacheDto MapToDto(MajorSkillsCache e) => new()
        {
            Id = e.Id,
            SearchKey = e.SearchKey,
            Faculty = e.Faculty,
            Major = e.Major,
            Level = e.Level,
            Country = e.Country,
            ResultJson = e.ResultJson,
            PromptVersion = e.PromptVersion,
            ModelName = e.ModelName,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
