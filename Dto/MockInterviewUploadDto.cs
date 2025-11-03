using System.Collections.Generic;
using System;

namespace FrancProject.Dto
{
    public class MockInterviewUploadDto
    {
        public int UserId { get; set; }
        public List<IFormFile> Videos { get; set; }
        public List<int> QuestionIds { get; set; }
        public string Duration { get; set; }

        public int? NbOfTry { get; set; }
    }
}
