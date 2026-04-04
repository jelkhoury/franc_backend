using FrancProject.DTOs;

namespace FrancProject.Interfaces
{
    public interface IMajorSkillsRepository
    {
        Task<MajorSkillsCacheDto?> CheckAsync(string searchKey);
        Task StoreAsync(StoreMajorSkillsRequestDto request);
    }
}
