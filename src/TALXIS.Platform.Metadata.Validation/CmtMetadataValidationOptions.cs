namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Options for <see cref="CmtMetadataValidator"/>.
/// </summary>
public sealed class CmtMetadataValidationOptions
{
    /// <summary>
    /// Severity for what the metadata may simply not cover: a table it does not contain, and a column or many-to-many relationship of a table
    /// it holds only in part (without its primary id column, as a solution holds a table another solution owns). Lower it to
    /// <see cref="ValidationSeverity.Warning"/> when the metadata comes from solutions rather than the whole target environment.
    /// </summary>
    public ValidationSeverity IncompleteMetadataSeverity { get; set; } = ValidationSeverity.Error;
}
