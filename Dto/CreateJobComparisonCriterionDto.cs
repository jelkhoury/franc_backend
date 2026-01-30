namespace FrancProject.Dto
{
    public class CreateJobComparisonCriterionDto
    {
        public string Name { get; set; }
        public string Section { get; set; }
        public string Category { get; set; } // HEAD / HEART
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
