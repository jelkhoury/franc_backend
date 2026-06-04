using Microsoft.AspNetCore.Http;

namespace FrancProject.Interfaces;

public enum FileUploadCategory
{
    Video,
    Document,
    Image,
    Spreadsheet,
    WordReport
}

public interface IFileUploadSecurityService
{
    Task ValidateAsync(IFormFile file, FileUploadCategory category, CancellationToken cancellationToken = default);

    Task ValidateBatchAsync(
        IEnumerable<IFormFile> files,
        FileUploadCategory category,
        long? maxTotalBytes = null,
        CancellationToken cancellationToken = default);
}
