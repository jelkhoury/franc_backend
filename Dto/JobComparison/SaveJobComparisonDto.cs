namespace FrancProject.Dto
{
    public class SaveJobComparisonDto
    {
        public int JobComparisonId { get; set; } // 0 = new
        public string JobAName { get; set; } = null!;
        public string JobBName { get; set; } = null!;
        public bool IsCompleted { get; set; }
        public List<JobComparisonAnswerDto> Answers { get; set; } = new();
    }

}
