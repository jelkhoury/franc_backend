using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class SubmitSDSResponseItemDto
    {
        public int QuestionId { get; set; }
        public string? SelectedValue { get; set; }   // <-- NEW
        public string? CustomAnswer { get; set; }
    }
}
