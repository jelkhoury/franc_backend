using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class SDSSection
    {
      
        public int Id { get; set; }

      
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;

        public ICollection<SDSQuestion> Questions { get; set; } = new List<SDSQuestion>();
    }
}
