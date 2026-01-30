namespace FrancProject.Dto
{
    public class SaveJobComparisonDto
    {
        public int JobComparisonId { get; set; } // 0 = new
        public string JobAName { get; set; }
        public string JobBName { get; set; }
        public bool IsCompleted { get; set; }
        public List<JobComparisonAnswerDto> Answers { get; set; } = new();
    }

}
