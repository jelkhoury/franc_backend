using System.ComponentModel.DataAnnotations;

namespace FrancProject.Models
{
    public class EvaluationReportSkill
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EvaluationReportId { get; set; }
        public EvaluationReport EvaluationReport { get; set; }

        [Required]
        [MaxLength(10)]
        public string SkillCode { get; set; }

        [Required]
        [MaxLength(255)]
        public string SkillName { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
    }
}
