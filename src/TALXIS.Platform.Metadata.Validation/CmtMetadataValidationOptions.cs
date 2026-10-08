namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Options for <see cref="CmtMetadataValidator"/>.
/// </summary>
public sealed class CmtMetadataValidationOptions
{
    /// <summary>
    /// Severity for a table the metadata does not contain. Lower it to <see cref="ValidationSeverity.Warning"/> when the metadata
    /// covers only some tables, for example a solution that does not include the system tables the package also carries.
    /// </summary>
    public ValidationSeverity UnknownEntitySeverity { get; set; } = ValidationSeverity.Error;
}
