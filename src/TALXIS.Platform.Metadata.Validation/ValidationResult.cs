namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// A single validation finding (error or warning) with optional source location.
/// </summary>
public sealed record ValidationResult(
    ValidationSeverity Severity,
    string Message,
    string? FilePath,
    int? Line,
    int? Column
);

public enum ValidationSeverity
{
    Error,
    Warning
}
