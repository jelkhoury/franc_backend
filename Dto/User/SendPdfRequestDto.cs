namespace FrancProject.Dto
{
    public class SendPdfRequestDto
    {
        public int UserId { get; set; }
        public string PdfFileName { get; set; } = null!;
        public string PdfBase64 { get; set; } = null!;
    }
}
