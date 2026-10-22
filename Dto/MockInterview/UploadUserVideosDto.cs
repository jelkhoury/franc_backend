using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace FrancProject.Dto
{
    public class UploadUserVideosDto
    {
        public int UserId { get; set; }
        public List<IFormFile> Videos { get; set; } = new();
        public List<int> QuestionIds { get; set; } = new();
        public string Duration { get; set; } = null!;

    }
}