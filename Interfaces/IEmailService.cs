namespace FrancProject.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken = default);

    Task SendMockInterviewSubmittedNotificationAsync(
        string adminEmail,
        string submitterFullName,
        CancellationToken cancellationToken = default);

    Task SendMockInterviewReportAsync(
        string toEmail,
        string firstName,
        byte[] documentBytes,
        string documentFileName,
        CancellationToken cancellationToken = default);
}
