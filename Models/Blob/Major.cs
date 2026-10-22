using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Text.Json.Serialization; 

namespace FrancProject.Models
{
    public class Major
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string UrlImage { get; set; } = null!;

        public int FacultyId { get; set; }

        [JsonIgnore]
        public Faculty Faculty { get; set; } = null!;

        [JsonIgnore]
        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
