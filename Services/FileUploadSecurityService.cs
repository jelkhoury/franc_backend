using FrancProject.Interfaces;
using FrancProject.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Text;

namespace FrancProject.Services;

public class FileUploadSecurityService : IFileUploadSecurityService
{
    private static readonly string[] BlockedExtensions =
    {
        ".exe", ".bat", ".cmd", ".com", ".msi", ".dll", ".scr", ".ps1", ".vbs", ".js", ".jar",
        ".html", ".htm", ".svg", ".php", ".asp", ".aspx", ".sh", ".bash"
    };

    private readonly UploadSecurityOptions _options;
    private readonly ILogger<FileUploadSecurityService> _logger;

    public FileUploadSecurityService(
        IOptions<UploadSecurityOptions> options,
        ILogger<FileUploadSecurityService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ValidateAsync(IFormFile file, FileUploadCategory category, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty.");

        var extension = NormalizeExtension(file.FileName);
        EnsureExtensionAllowed(extension, category);

        var maxBytes = GetMaxBytes(category);
        if (file.Length > maxBytes)
            throw new ArgumentException($"File '{file.FileName}' exceeds the maximum allowed size of {maxBytes / (1024 * 1024)} MB.");

        await using var stream = file.OpenReadStream();
        if (!await HasValidSignatureAsync(stream, extension, category, cancellationToken))
            throw new ArgumentException($"File '{file.FileName}' content does not match its type.");

        if (_options.EnableClamAvScan)
            await ScanWithClamAvAsync(stream, file.FileName, cancellationToken);

        _logger.LogInformation(
            "Upload accepted. File={FileName} Category={Category} Size={SizeBytes}",
            file.FileName,
            category,
            file.Length);
    }

    public async Task ValidateBatchAsync(
        IEnumerable<IFormFile> files,
        FileUploadCategory category,
        long? maxTotalBytes = null,
        CancellationToken cancellationToken = default)
    {
        var list = files?.Where(f => f.Length > 0).ToList() ?? [];
        if (list.Count == 0)
            throw new ArgumentException("No files provided.");

        long total = 0;
        foreach (var file in list)
        {
            await ValidateAsync(file, category, cancellationToken);
            total += file.Length;
        }

        if (maxTotalBytes.HasValue && total > maxTotalBytes.Value)
            throw new ArgumentException($"Total upload size ({total / (1024 * 1024)} MB) exceeds the allowed limit.");
    }

    private long GetMaxBytes(FileUploadCategory category) => category switch
    {
        FileUploadCategory.Video => _options.MaxVideoBytes,
        FileUploadCategory.Document => _options.MaxDocumentBytes,
        FileUploadCategory.Image => _options.MaxImageBytes,
        FileUploadCategory.Spreadsheet => _options.MaxSpreadsheetBytes,
        FileUploadCategory.WordReport => _options.MaxWordReportBytes,
        _ => _options.MaxDocumentBytes
    };

    private static string NormalizeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
            throw new ArgumentException("File must have an extension.");
        return ext.ToLowerInvariant();
    }

    private static void EnsureExtensionAllowed(string extension, FileUploadCategory category)
    {
        if (BlockedExtensions.Contains(extension))
            throw new ArgumentException($"File type '{extension}' is not allowed.");

        var allowed = category switch
        {
            FileUploadCategory.Video => new[] { ".mp4", ".webm", ".mov", ".m4v" },
            FileUploadCategory.Image => new[] { ".jpg", ".jpeg", ".png", ".webp" },
            FileUploadCategory.Spreadsheet => new[] { ".xlsx" },
            FileUploadCategory.WordReport => new[] { ".docx" },
            FileUploadCategory.Document => new[] { ".pdf", ".doc", ".docx" },
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(extension))
            throw new ArgumentException($"Extension '{extension}' is not allowed for {category} uploads.");
    }

    private static async Task<bool> HasValidSignatureAsync(
        Stream stream,
        string extension,
        FileUploadCategory category,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (read < 4)
            return false;

        return extension switch
        {
            ".mp4" or ".m4v" or ".mov" => read >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t'
                                        && header[6] == (byte)'y' && header[7] == (byte)'p',
            ".webm" => header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
            ".pdf" => header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F',
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".webp" => read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I'
                       && header[2] == (byte)'F' && header[3] == (byte)'F'
                       && header[8] == (byte)'W' && header[9] == (byte)'E'
                       && header[10] == (byte)'B' && header[11] == (byte)'P',
            ".docx" or ".xlsx" => header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07)
                                  && (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08),
            ".doc" => header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0,
            _ => false
        };
    }

    private async Task ScanWithClamAvAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        stream.Position = 0;

        try
        {
            using var client = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ClamAvTimeoutSeconds));
            await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, connectCts.Token);

            await using var network = client.GetStream();
            await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), cancellationToken);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                var sizeBytes = BitConverter.GetBytes(bytesRead);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(sizeBytes);

                await network.WriteAsync(sizeBytes.AsMemory(0, 4), cancellationToken);
                await network.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await network.WriteAsync(new byte[4], cancellationToken);

            using var reader = new StreamReader(network, Encoding.ASCII);
            var response = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;

            if (response.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("ClamAV detected threat in {FileName}. Response={Response}", fileName, response);
                throw new InvalidOperationException("File failed virus scan and was rejected.");
            }

            if (!response.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected ClamAV response: {response}");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed for {FileName}", fileName);
            throw new InvalidOperationException("Virus scan is unavailable. Upload rejected.", ex);
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
