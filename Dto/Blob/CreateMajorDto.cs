namespace FrancProject.Dto
{
    public class CreateMajorDto
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string UrlImage { get; set; } = null!;
        public int FacultyId { get; set; }
    }
}
