namespace FrancProject.Dto
{
    public class QuestionWithMajorDto
    {
        public int QuestionId { get; set; }
        public string Title { get; set; } = null!;
        public string VideoUrl { get; set; } = null!;
        public int MajorId { get; set; }
        public string? MajorName { get; set; }
    }
}
