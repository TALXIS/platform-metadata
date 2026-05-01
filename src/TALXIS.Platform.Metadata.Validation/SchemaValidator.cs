using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates XML documents against the embedded XSD schema set.
/// </summary>
public sealed class SchemaValidator
{
    private readonly XmlSchemaSet _schemas;

    /// <summary>
    /// Loads all embedded XSD schemas and compiles the schema set.
    /// </summary>
    public SchemaValidator()
    {
        _schemas = new XmlSchemaSet();
        foreach (var schemaFile in SchemaResourceLoader.GetAvailableSchemas()
                     .Where(f => f.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = SchemaResourceLoader.OpenSchema(schemaFile);
            _schemas.Add(null, XmlReader.Create(stream));
        }
        _schemas.Compile();
    }

    /// <summary>
    /// Validates a file on disk. The file is loaded, nil-normalized, then
    /// validated against the schema set.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new[]
            {
                new ValidationResult(ValidationSeverity.Error, $"File not found: {filePath}", filePath, null, null)
            };
        }

        try
        {
            var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo);
            return ValidateXml(doc, filePath);
        }
        catch (XmlException ex)
        {
            return new[]
            {
                new ValidationResult(ValidationSeverity.Error, ex.Message, filePath, ex.LineNumber, ex.LinePosition)
            };
        }
    }

    /// <summary>
    /// Validates an in-memory <see cref="XDocument"/> against the schema set.
    /// Nil elements are normalized before validation to avoid false positives.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateXml(XDocument document, string? sourcePath = null)
    {
        XmlNormalizer.NormalizeNilElements(document);

        var results = new List<ValidationResult>();

        var settings = new XmlReaderSettings
        {
            ValidationType  = ValidationType.Schema,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                            | XmlSchemaValidationFlags.ProcessIdentityConstraints,
            Schemas         = _schemas,
            DtdProcessing   = DtdProcessing.Ignore,
            XmlResolver     = null
        };

        settings.ValidationEventHandler += (_, e) =>
        {
            var severity = e.Severity == XmlSeverityType.Error
                ? ValidationSeverity.Error
                : ValidationSeverity.Warning;

            int? line = e.Exception?.LineNumber is > 0 ? e.Exception.LineNumber : null;
            int? col  = e.Exception?.LinePosition is > 0 ? e.Exception.LinePosition : null;

            results.Add(new ValidationResult(severity, e.Message, sourcePath, line, col));
        };

        try
        {
            using var nodeReader = document.CreateReader();
            using var reader = XmlReader.Create(nodeReader, settings);
            while (reader.Read()) { /* drives validation */ }
        }
        catch (XmlException ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error, ex.Message, sourcePath, ex.LineNumber, ex.LinePosition));
        }

        return results;
    }
}
