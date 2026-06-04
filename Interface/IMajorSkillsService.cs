using FrancProject.Dto;

namespace FrancProject.Interfaces
{
    public interface IMajorSkillsService
    {
        Task<MajorSkillsCacheDto?> CheckAsync(string searchKey);
        Task StoreAsync(StoreMajorSkillsRequestDto request);
    }
}
