namespace TALXIS.Platform.Metadata.Validation;

/// <summary>Builds <see cref="ValidationResult"/>s located at a CMT model element's source position.</summary>
internal static class CmtFindings
{
    public static ValidationResult Error(MetadataBase element, string code, string message) =>
        Finding(ValidationSeverity.Error, element, code, message);

    public static ValidationResult Finding(ValidationSeverity severity, MetadataBase element, string code, string message)
    {
        var source = element.Source;
        var path = string.IsNullOrEmpty(source?.FilePath) ? null : source!.FilePath;
        return new ValidationResult(severity, message, path, source?.Line, source?.Column) { Code = code };
    }
}
