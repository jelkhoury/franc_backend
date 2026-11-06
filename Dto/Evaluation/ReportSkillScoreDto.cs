namespace FrancProject.Dto
{
    public class ReportSkillScoreDto
    {
        public string SkillCode { get; set; } = null!;
        public string SkillName { get; set; } = null!;
        public int Rating { get; set; }
    }

    public class ReportSkillScoreInputDto
    {
        public string SkillCode { get; set; } = null!;
        public string SkillName { get; set; } = null!;
        public int Rating { get; set; }
    }
}
