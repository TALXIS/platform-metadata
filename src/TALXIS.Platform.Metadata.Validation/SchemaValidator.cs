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
    public IReadOnlyList<ValidationResult> ValidateFile(string filePath) => ValidateFile(filePath, null);

    /// <summary>
    /// Validates a file on disk, skipping it without a finding when no XSD declares its root element
    /// and <paramref name="isComponentInstance"/> recognises the file as a solution component instance
    /// (a solution-aware table record or SCF component), whose shape is defined by its table rather than an XSD.
    /// </summary>
    internal IReadOnlyList<ValidationResult> ValidateFile(string filePath, Func<XElement, bool>? isComponentInstance)
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
            if (doc.Root != null && !DeclaresRootElement(doc.Root.Name) && isComponentInstance?.Invoke(doc.Root) == true)
                return Array.Empty<ValidationResult>();

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
        // Without a declaration the reader reports the root as "not declared", an error that would fail
        // every component type the schemas do not cover yet. Report it once, as a suppressible warning.
        if (document.Root != null && !DeclaresRootElement(document.Root.Name))
            return new[] { SchemaNotFound(document.Root, sourcePath) };

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

    private bool DeclaresRootElement(XName name) =>
        _schemas.GlobalElements.Contains(new XmlQualifiedName(name.LocalName, name.NamespaceName));

    private static ValidationResult SchemaNotFound(XElement root, string? sourcePath)
    {
        var lineInfo = (IXmlLineInfo)root;
        return new ValidationResult(
            ValidationSeverity.Warning,
            $"No schema declares the root element '{root.Name.LocalName}', so the file is not schema-validated. " +
            "Expected for a component type the validator does not know yet; otherwise check the root element name.",
            sourcePath,
            lineInfo.HasLineInfo() ? lineInfo.LineNumber : null,
            lineInfo.HasLineInfo() ? lineInfo.LinePosition : null) { Code = ValidationDiagnostics.SchemaNotFound };
    }
}
