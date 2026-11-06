namespace FrancProject.Dto
{
    public class JobComparisonCriterionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Section { get; set; } = null!;
        public string Category { get; set; } = null!; // HEAD / HEART
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }

}
