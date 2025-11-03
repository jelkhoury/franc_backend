using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class SubmitSDSResponsesDto
    {
        public int UserId { get; set; }
        public List<SubmitSDSResponseItemDto> Responses { get; set; }
    }
}
