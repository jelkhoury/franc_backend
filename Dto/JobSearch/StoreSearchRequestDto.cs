namespace FrancProject.Dto;

public class StoreSearchRequestDto
{
    public string SearchKey { get; set; } = null!;
    public string QueryText { get; set; } = null!;
    public string LocationUsed { get; set; } = null!;
    public List<JobPostDto> Jobs { get; set; } = new();
}
