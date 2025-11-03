using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;  

namespace FrancProject.Models
{
    public class Question
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        public string VideoUrl { get; set; }

        [JsonIgnore]
        public int MajorId { get; set; }
        public Major Major { get; set; }
    }
}
