namespace FrancProject.Dto;

public class SendWordReportFormDto
{
    public int UserId { get; set; }
    public IFormFile ReportFile { get; set; } = null!;
}
