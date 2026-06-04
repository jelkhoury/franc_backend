using FrancProject.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace FrancProject.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public Task SendVerificationCodeAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(
            toEmail,
            "Verification Code",
            $"Your verification code is: <b>{code}</b>");

        return SendAsync(message, cancellationToken);
    }

    public Task SendMockInterviewSubmittedNotificationAsync(
        string adminEmail,
        string submitterFullName,
        CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(
            adminEmail,
            "Mock Interview Submitted",
            $"Mock interview was submitted by <b>{submitterFullName}</b>.");

        return SendAsync(message, cancellationToken);
    }

    public Task SendMockInterviewReportAsync(
        string toEmail,
        string firstName,
        byte[] documentBytes,
        string documentFileName,
        CancellationToken cancellationToken = default)
    {
        if (documentBytes == null || documentBytes.Length == 0)
            throw new ArgumentException("Report document is empty.");

        var attachmentName = EnsureDocxFileName(documentFileName);

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]!));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = "Mock Interview Evaluation Report – CCD Department";

        var builder = new BodyBuilder
        {
            HtmlBody = BuildMockInterviewReportEmailBody(firstName)
        };

        builder.Attachments.Add(
            attachmentName,
            documentBytes,
            ContentType.Parse("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));

        email.Body = builder.ToMessageBody();

        return SendAsync(email, cancellationToken);
    }

    private MimeMessage CreateMessage(string toEmail, string subject, string htmlBody)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["MAIL_FROM_ADDRESS"]!));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        email.Body = new TextPart(TextFormat.Html) { Text = htmlBody };
        return email;
    }

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _config["MAIL_HOST"]!,
            int.Parse(_config["MAIL_PORT"]!),
            SecureSocketOptions.StartTls,
            cancellationToken);
        await smtp.AuthenticateAsync(
            _config["MAIL_USERNAME"]!,
            _config["MAIL_PASSWORD"]!,
            cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    private static string EnsureDocxFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "MockInterviewReport.docx";

        return Path.GetExtension(fileName).Equals(".docx", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : Path.ChangeExtension(fileName, ".docx");
    }

    private static string BuildMockInterviewReportEmailBody(string firstName)
    {
        var studentName = string.IsNullOrWhiteSpace(firstName)
            ? "Student"
            : firstName.Trim();

        return $"""
            <div style="font-family: Arial, Helvetica, sans-serif; color: #222; line-height: 1.6; max-width: 640px;">
                <p>Dear {studentName},</p>
                <p>
                    We hope this message finds you well. The Career and Community Development (CCD) Department has completed
                    its review of your mock interview session conducted through <strong>Franc</strong>.
                </p>
                <p>
                    Please find attached your official evaluation report, which summarizes your performance, feedback,
                    and recommendations to help you prepare for future interviews.
                </p>
                <p>
                    We encourage you to review the report carefully and use the feedback to continue developing your
                    interview skills. If you have any questions or would like to discuss your results further, please
                    contact the CCD Department.
                </p>
                <p>
                    Best regards,<br/>
                    <strong>Career and Community Development (CCD) Department</strong><br/>
                    University Career Services
                </p>
                <p style="font-size: 12px; color: #666; margin-top: 24px;">
                    This message was sent following the completion of your Franc mock interview evaluation.
                </p>
            </div>
            """;
    }
}
