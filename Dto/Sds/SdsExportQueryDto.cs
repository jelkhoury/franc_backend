namespace FrancProject.Dto;

public class SdsExportQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>all | complete | incomplete</summary>
    public string? Completion { get; set; } = "all";
}

public enum SdsExportCompletionFilter
{
    All,
    Complete,
    Incomplete
}

public sealed class SdsExcelExportResult
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
}

public sealed class SdsExportRow
{
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string? HollandCode { get; init; }
    public bool IsCompleted { get; init; }
    public int AttemptNumber { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
