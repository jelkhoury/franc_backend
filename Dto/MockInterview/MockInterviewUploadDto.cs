using System.Collections.Generic;
using System;

namespace FrancProject.Dto
{
    public class MockInterviewUploadDto
    {
        public int UserId { get; set; }
        public List<IFormFile> Videos { get; set; } = new();
        public List<int> QuestionIds { get; set; } = new();
        public string Duration { get; set; } = null!;

        public int? NbOfTry { get; set; }
    }
}
