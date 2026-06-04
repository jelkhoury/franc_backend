namespace FrancProject.Dto
{
    public class JobComparisonCriterionDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Section { get; set; }
        public string Category { get; set; } // HEAD / HEART
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }

}
