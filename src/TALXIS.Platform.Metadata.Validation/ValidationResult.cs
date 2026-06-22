namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// A single validation finding (error or warning) with optional source location.
/// </summary>
/// <param name="Severity">Severity of the finding.</param>
/// <param name="Message">Human-readable validation message.</param>
/// <param name="FilePath">Path to the source file that produced the finding, or <see langword="null"/> for directory-level errors.</param>
/// <param name="Line">1-based line number when the finding can be mapped precisely, otherwise <see langword="null"/>.</param>
/// <param name="Column">1-based column number when the finding can be mapped precisely, otherwise <see langword="null"/>.</param>
public sealed record ValidationResult(
    ValidationSeverity Severity,
    string Message,
    string? FilePath,
    int? Line,
    int? Column
)
{
    public ValidationStage Stage { get; init; } = ValidationStage.Workspace;
}

/// <summary>
/// Severity level for a <see cref="ValidationResult"/>.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Validation failed and should block the operation.
    /// </summary>
    Error,

    /// <summary>
    /// Validation detected a non-fatal problem that should be surfaced to the user.
    /// </summary>
    Warning
}

public enum ValidationStage
{
    Workspace,
    Schema,
    Json,
    DuplicateGuid,
    ModelLoad,
    Flow,
    Relationship
}

public static class ValidationStageExtensions
{
    public static string Label(this ValidationStage stage) => stage switch
    {
        ValidationStage.Schema => "Schema",
        ValidationStage.Json => "JSON",
        ValidationStage.DuplicateGuid => "GUID",
        ValidationStage.ModelLoad => "Model",
        ValidationStage.Flow => "Flow",
        ValidationStage.Relationship => "Relationship",
        _ => "Workspace"
    };
}
