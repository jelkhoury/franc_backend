using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrancProject.Models
{
    public class SDSSection
    {
      
        public int Id { get; set; }

      
        public string Name { get; set; } 
        public string Description { get; set; }

        public ICollection<SDSQuestion> Questions { get; set; }
    }
}
