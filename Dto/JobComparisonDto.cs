namespace FrancProject.Dto
{
    public class JobComparisonDto
    {
        public int Id { get; set; }
        public string JobAName { get; set; }
        public int UserId { get; set; }
        public string JobBName { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ExcelResultUrl { get; set; }
        public List<JobComparisonAnswerDto> Answers { get; set; } = new();
    }

}
