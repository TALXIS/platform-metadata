using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates Configuration Migration Tool data schema files (data_schema.xml): every entity
/// must declare at least one field with updateCompare="true", otherwise imports cannot match
/// existing records and re-deploys duplicate configuration data instead of updating it.
/// Structural rules check that the schema is consistent with itself: import order, primary
/// id and name fields, lookup targets and duplicate names.
/// </summary>
public sealed class CmtDataSchemaValidator
{
    // Field types whose schema entry names the target entity through lookupType. Owner is deliberately
    // absent: CMT never writes lookupType for owner fields (0 of 219 owner fields in production exports),
    // because ownership is always systemuser|team. CMT compares type names ordinally, so a capitalised
    // "EntityReference" is not a lookup to it either.
    private static readonly HashSet<string> LookupTypes = new(StringComparer.Ordinal)
    {
        CmtFieldTypes.EntityReference, CmtFieldTypes.Customer
    };

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
    /// &lt;entities&gt; element are skipped, as are data files (data.xml) whose
    /// entities carry records instead of field declarations.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateXml(XDocument document, string? sourcePath = null)
    {
        var root = document.Root;
        if (root == null || root.Name.LocalName != "entities") return Array.Empty<ValidationResult>();

        // data.xml shares the <entities> root; only schema-form entities declare <fields>.
        var isSchema = root.Elements().Where(e => e.Name.LocalName == "entity").Any(e => e.Elements().Any(c => c.Name.LocalName == "fields"));
        if (!isSchema) return Array.Empty<ValidationResult>();

        return Validate(new CmtPackageXmlReader().ReadSchema(document, sourcePath))
            .Select(r => r.FilePath is null ? r with { FilePath = sourcePath } : r)
            .ToList();
    }

    /// <summary>
    /// Validates a CMT data schema model, for example one built in memory before it is saved.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(CmtDataSchema schema)
    {
        var results = new List<ValidationResult>();

        foreach (var entity in schema.Entities)
        {
            if (!entity.Fields.Any(f => f.IsUpdateCompare))
            {
                results.Add(Error(entity, ValidationDiagnostics.CmtEntityMissingUpdateCompare,
                    $"CMT data schema entity '{entity.Name}' declares no field with updateCompare=\"true\". Without it configuration imports cannot match existing records and re-deploys duplicate data."));
            }

            CheckPrimaryIdField(entity, results);
            CheckPrimaryNameField(entity, results);
            CheckLookupTypes(entity, results);
            CheckDuplicateFields(entity, results);
        }

        CheckDuplicateEntities(schema, results);
        CheckImportOrder(schema, results);
        return results;
    }

    private static void CheckPrimaryIdField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryIdField))
        {
            results.Add(Error(entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data schema entity '{entity.Name}' has no primaryidfield, so imported records cannot be identified."));
            return;
        }

        var field = entity.FindField(entity.PrimaryIdField!);
        string? problem =
            field is null ? "is not declared in <fields>"
            : !field.IsPrimaryKey ? "is not marked primaryKey=\"true\""
            : !string.Equals(field.Type, CmtFieldTypes.Guid, StringComparison.Ordinal) ? $"has type '{field.Type}' instead of 'guid'"
            : null;
        if (problem is null) return;

        results.Add(Error((MetadataBase?)field ?? entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
            $"CMT data schema entity '{entity.Name}': primaryidfield '{entity.PrimaryIdField}' {problem}."));
    }

    private static void CheckPrimaryNameField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryNameField) || entity.FindField(entity.PrimaryNameField!) is not null) return;

        results.Add(Finding(ValidationSeverity.Warning, entity, ValidationDiagnostics.CmtPrimaryNameFieldUndeclared,
            $"CMT data schema entity '{entity.Name}': primarynamefield '{entity.PrimaryNameField}' is not declared in <fields>."));
    }

    private static void CheckLookupTypes(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        foreach (var field in entity.Fields.Where(f => f.Type is not null && LookupTypes.Contains(f.Type) && string.IsNullOrEmpty(f.LookupType)))
        {
            results.Add(Finding(ValidationSeverity.Warning, field, ValidationDiagnostics.CmtLookupTypeMissing,
                $"CMT data schema field '{entity.Name}.{field.Name}' is a lookup ({field.Type}) without lookupType, so the schema does not say which entity it points to; only the lookupentity on each record does."));
        }
    }

    private static void CheckDuplicateFields(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(entity.Fields, f => f.Name))
        {
            results.Add(Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data schema entity '{entity.Name}' declares field '{duplicate.Name}' more than once."));
        }
    }

    private static void CheckDuplicateEntities(CmtDataSchema schema, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(schema.Entities, e => e.Name))
        {
            results.Add(Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data schema declares entity '{duplicate.Name}' more than once."));
        }
    }

    // CMT falls back to the order of <entity> elements when entityImportOrder is absent, so only a present list is checked.
    private static void CheckImportOrder(CmtDataSchema schema, List<ValidationResult> results)
    {
        if (schema.EntityImportOrder.Count == 0) return;

        var ordered = new HashSet<string>(schema.EntityImportOrder, StringComparer.Ordinal);
        foreach (var name in schema.EntityImportOrder.Where(n => schema.FindEntity(n) is null))
        {
            var caseMatch = schema.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (caseMatch is not null)
            {
                results.Add(Finding(ValidationSeverity.Warning, schema, ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT entityImportOrder names entity '{name}', but the data schema declares it as '{caseMatch.Name}'. CMT compares names case-sensitively and will not find it."));
                continue;
            }

            results.Add(Error(schema, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT entityImportOrder names entity '{name}', which the data schema does not declare."));
        }

        foreach (var entity in schema.Entities.Where(e => !ordered.Contains(e.Name)))
        {
            results.Add(Finding(ValidationSeverity.Warning, entity, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT data schema entity '{entity.Name}' is missing from entityImportOrder, so its import position is undefined."));
        }
    }

    private static IEnumerable<T> Duplicates<T>(IEnumerable<T> items, Func<T, string> name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(item => !seen.Add(name(item)));
    }

    internal static ValidationResult Error(MetadataBase element, string code, string message) =>
        Finding(ValidationSeverity.Error, element, code, message);

    internal static ValidationResult Finding(ValidationSeverity severity, MetadataBase element, string code, string message)
    {
        var source = element.Source;
        var path = string.IsNullOrEmpty(source?.FilePath) ? null : source!.FilePath;
        return new ValidationResult(severity, message, path, source?.Line, source?.Column) { Code = code };
    }
}
