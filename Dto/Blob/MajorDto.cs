namespace FrancProject.Dto
{
    public class MajorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string UrlImage { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int FacultyId { get; set; }
    }
}
