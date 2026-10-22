using System.Collections.Generic;

namespace FrancProject.Dto
{

    public  class CreateSDSAnswerOptionDto
    {
        public string Text { get; set; } = null!;   // e.g., "Like"
        public string? Value { get; set; }  // e.g., "R", "I", "A", "S", "E", "C" or null
    }
}

