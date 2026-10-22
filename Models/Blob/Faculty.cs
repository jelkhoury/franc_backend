using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace FrancProject.Models
{
    public class Faculty
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        public ICollection<Major> Majors { get; set; } = new List<Major>();
    }
}
