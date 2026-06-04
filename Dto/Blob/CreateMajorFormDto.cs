namespace FrancProject.Dto;

public class CreateMajorFormDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IFormFile Image { get; set; } = null!;
    public int FacultyId { get; set; }
}
