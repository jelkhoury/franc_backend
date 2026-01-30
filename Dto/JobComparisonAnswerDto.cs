namespace FrancProject.Dto
{
    public class JobComparisonAnswerDto
    {
        public int CriterionId { get; set; }
        public int UserId { get; set; }
        public int Weight { get; set; }
        public int ScoreA { get; set; }
        public int ScoreB { get; set; }
        public bool NotApplicable { get; set; }
    }

}
