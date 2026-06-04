namespace FrancProject.Dto
{
    public class SendPdfRequestDto
    {
        public int UserId { get; set; }
        public string PdfFileName { get; set; }
        public string PdfBase64 { get; set; } 
    }
}
