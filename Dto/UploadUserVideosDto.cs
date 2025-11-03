using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace FrancProject.Dto
{
    public class UploadUserVideosDto
    {
        public int UserId { get; set; }
        public List<IFormFile> Videos { get; set; }
        public List<int> QuestionIds { get; set; }
        public string Duration { get; set; }

    }
}