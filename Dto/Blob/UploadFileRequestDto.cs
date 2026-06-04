using Microsoft.AspNetCore.Http;

namespace FrancProject.Dto
{
    public class UploadFileRequestDto
    {
        public int UserId { get; set; }
        public string Title { get; set; }
        public string FolderName { get; set; }

        public IFormFile? ResumeFile { get; set; }
        public IFormFile? CoverFile { get; set; }
        public IFormFile? JobAddFile { get; set; }

        public string? AiEvaluation { get; set; }
    }
}
