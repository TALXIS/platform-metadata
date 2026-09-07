using System.Xml;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates Configuration Migration Tool data schema files (data_schema.xml): every entity
/// must declare at least one field with updateCompare="true", otherwise imports cannot match
/// existing records and re-deploys duplicate configuration data instead of updating it.
/// </summary>
public sealed class CmtDataSchemaValidator
{
    /// <summary>
    /// Validates a file on disk. Files that are not CMT data schemas are skipped.
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
        catch (XmlException)
        {
            // Malformed XML is already reported by the schema validation stage;
            // repeating the parse error here would just duplicate the finding.
            return Array.Empty<ValidationResult>();
        }
    }

    /// <summary>
    /// Validates an in-memory document. Documents whose root is not the CMT
    /// &lt;entities&gt; element are skipped, as are data-form entities (record
    /// payloads) that carry no field declarations.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateXml(XDocument document, string? sourcePath = null)
    {
        var root = document.Root;
        if (root == null || root.Name.LocalName != "entities")
            return Array.Empty<ValidationResult>();

        var results = new List<ValidationResult>();

        foreach (var entity in root.Elements().Where(e => e.Name.LocalName == "entity"))
        {
            // Schema-form entities declare <fields>; data-form entities (data.xml) declare
            // <records> instead and are not subject to the updateCompare rule.
            var fields = entity.Elements().FirstOrDefault(e => e.Name.LocalName == "fields");
            if (fields == null) continue;

            var hasUpdateCompare = fields.Elements()
                .Where(e => e.Name.LocalName == "field")
                .Any(f => string.Equals(f.Attribute("updateCompare")?.Value, "true", StringComparison.OrdinalIgnoreCase));

            if (hasUpdateCompare) continue;

            var entityName = entity.Attribute("name")?.Value ?? "(unnamed)";
            var lineInfo = (IXmlLineInfo)entity;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"CMT data schema entity '{entityName}' declares no field with updateCompare=\"true\". Without it configuration imports cannot match existing records and re-deploys duplicate data.",
                sourcePath,
                lineInfo.HasLineInfo() ? lineInfo.LineNumber : null,
                lineInfo.HasLineInfo() ? lineInfo.LinePosition : null)
            {
                Code = ValidationDiagnostics.CmtEntityMissingUpdateCompare
            });
        }

        return results;
    }
}
