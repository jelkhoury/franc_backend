namespace FrancProject.Options;

public class UploadSecurityOptions
{
    public const string SectionName = "UploadSecurity";

    /// <summary>Global HTTP request body limit (Kestrel, IIS, multipart forms). Must exceed mock-interview batch total.</summary>
    public long MaxRequestBodySizeBytes { get; set; } = 1050 * 1024 * 1024;

    public long MaxVideoBytes { get; set; } = 50 * 1024 * 1024;
    public long MaxDocumentBytes { get; set; } = 15 * 1024 * 1024;
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxSpreadsheetBytes { get; set; } = 52 * 1024 * 1024;
    public long MaxWordReportBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Max answer videos in one POST upload-mock-interview (one per question).</summary>
    public int MaxVideosPerMockInterview { get; set; } = 20;

    /// <summary>Sum of all video sizes in one mock interview upload (20 × 50 MB cap).</summary>
    public long MaxMockInterviewBatchBytes { get; set; } = 1000 * 1024 * 1024;

    /// <summary>When true, requires ClamAV host and scans every uploaded file before storage.</summary>
    public bool EnableClamAvScan { get; set; }

    public string ClamAvHost { get; set; } = "127.0.0.1";
    public int ClamAvPort { get; set; } = 3310;
    public int ClamAvTimeoutSeconds { get; set; } = 60;
}
