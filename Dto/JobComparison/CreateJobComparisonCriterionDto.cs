namespace FrancProject.Dto
{
    public class CreateJobComparisonCriterionDto
    {
        public string Name { get; set; } = null!;
        public string Section { get; set; } = null!;
        public string Category { get; set; } = null!; // HEAD / HEART
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
