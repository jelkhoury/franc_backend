using System.Collections.Generic;

namespace FrancProject.Dto
{
    public class CreateQuestionDto
    {
        public string MajorName { get; set; } = null!;
        public List<IFormFile> QuestionVideos { get; set; } = new();
    }
}
