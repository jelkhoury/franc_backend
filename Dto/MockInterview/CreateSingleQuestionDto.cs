namespace FrancProject.Dto;

public class CreateSingleQuestionDto
{
    public string MajorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IFormFile Video { get; set; } = null!;
}
