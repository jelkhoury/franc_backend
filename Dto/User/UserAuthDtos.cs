namespace FrancProject.Dto;

public class SignInRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PdfEmailRequestDto
{
    public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
    public string PdfFileName { get; set; } = string.Empty;
}

public enum UserActionType
{
    MockInterview,
    SDS,
    Resume,
    CoverLetter
}
