namespace FrancProject.Dto
{
    public class AdminFileDto
    {
        public int Id { get; set; }
        public string Title { get; set; }

        public string? ResumeUrl { get; set; }
        public string? CoverUrl { get; set; }
        public string? JobAddUrl { get; set; }

        public string? AiEvaluation { get; set; }

        public string UserEmail { get; set; }
    }
}
