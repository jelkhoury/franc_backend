namespace FrancProject.Helpers;

public sealed class ApiErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public string? Details { get; init; }
}
