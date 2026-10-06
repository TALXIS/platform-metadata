using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Structural rules for Configuration Migration Tool data schema files (data_schema.xml): does the schema
/// hang together and will both Microsoft CMT and the TALXIS importer accept it. Rules are derived from the
/// importers' observed behaviour and need no metadata: record matching (TXM006), entityImportOrder
/// consistency (TXM007), primary id/name fields (TXM008, TXM009), duplicate names (TXM011), names that are
/// not lowercase (TXM015), importable field types (TXM016), dateMode values (TXM018) and FetchXML filters
/// (TXM020). Rules that need data.xml live in <see cref="CmtPackageValidator"/>; rules that need Dataverse
/// metadata are a separate validator.
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
            CheckUpdateCompare(entity, results);
            CheckLowercaseNames(entity, results);
            CheckPrimaryIdField(entity, results);
            CheckPrimaryNameField(entity, results);
            CheckFieldTypes(entity, results);
            foreach (var field in entity.Fields) CheckDateMode(field, field.DateMode, $"CMT data schema field '{entity.Name}.{field.Name}'", results);
            CheckFilter(entity, results);
            CheckDuplicateFields(entity, results);
        }

        CheckDateMode(schema, schema.DateMode, "CMT data schema root", results);
        CheckDuplicateEntities(schema, results);
        CheckImportOrder(schema, results);
        return results;
    }

    // Without updateCompare fields CMT matches existing records on the primary name column; without that either, re-imports duplicate every record.
    private static void CheckUpdateCompare(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (entity.Fields.Any(f => f.IsUpdateCompare)) return;

        results.Add(string.IsNullOrEmpty(entity.PrimaryNameField)
            ? CmtFindings.Error(entity, ValidationDiagnostics.CmtEntityMissingUpdateCompare,
                $"CMT data schema entity '{entity.Name}' declares no field with updateCompare=\"true\" and no primarynamefield, so CMT cannot match existing records and re-deploys duplicate data.")
            : CmtFindings.Warning(entity, ValidationDiagnostics.CmtEntityMissingUpdateCompare,
                $"CMT data schema entity '{entity.Name}' declares no field with updateCompare=\"true\". CMT matches existing records on the primary name '{entity.PrimaryNameField}', which may not be unique."));
    }

    // Dataverse logical names are lowercase and CMT looks them up case-sensitively: it rejects a package whose schema spells an entity or column otherwise.
    private static void CheckLowercaseNames(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (!IsLowercase(entity.Name))
        {
            results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtNameCaseMismatch,
                $"CMT data schema entity '{entity.Name}' is not lowercase. Dataverse logical names are lowercase and CMT compares them case-sensitively, so the import fails."));
        }

        foreach (var field in entity.Fields.Where(f => !IsLowercase(f.Name)))
        {
            results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtNameCaseMismatch,
                $"CMT data schema field '{entity.Name}.{field.Name}' is not lowercase. Dataverse logical names are lowercase and CMT rejects the package with 'Missing Fields'."));
        }
    }

    private static bool IsLowercase(string name) => string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal);

    // CMT's importer looks the type up ordinally in a fixed table; a misspelled or capitalised type and unknown have no conversion.
    private static void CheckFieldTypes(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        var fields = entity.Fields.Select(f => (Field: f, Owner: entity.Name))
            .Concat(entity.Relationships.SelectMany(r => r.Fields.Select(f => (Field: f, Owner: $"{entity.Name}/{r.Name}"))));

        foreach (var (field, owner) in fields)
        {
            var type = field.Type;
            string? problem;
            if (string.IsNullOrEmpty(type))
                problem = "has no type";
            else if (type == CmtFieldTypes.File)
            {
                results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                    $"CMT data schema field '{owner}.{field.Name}' has type 'file', a TALXIS synonym for 'filedata'. Microsoft CMT and txc reject the package; use 'filedata' for them."));
                continue;
            }
            else if (type == CmtFieldTypes.BigInt)
            {
                results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                    $"CMT data schema field '{owner}.{field.Name}' has type 'bigint'. CMT accepts the schema but drops the values on export and import."));
                continue;
            }
            else if (type == CmtFieldTypes.Customer)
                problem = "has type 'customer', which CMT rejects; use 'entityreference' with lookupType=\"account|contact\"";
            else if (type == CmtFieldTypes.Unknown)
                problem = "has type 'unknown', which CMT exports but cannot import (no conversion)";
            else if (CmtFieldTypes.Importable.Contains(type!))
                continue;
            else if (CmtFieldTypes.Importable.Contains(type!.ToLowerInvariant()))
                problem = $"has type '{type}' instead of '{type.ToLowerInvariant()}'; CMT compares type names case-sensitively";
            else
                problem = $"has type '{type}', which is not a CMT field type";

            results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                $"CMT data schema field '{owner}.{field.Name}' {problem}."));
        }
    }

    private static void CheckDateMode(MetadataBase element, string? dateMode, string subject, List<ValidationResult> results)
    {
        if (dateMode is null || CmtDateModes.All.Contains(dateMode)) return;
        results.Add(CmtFindings.Error(element, ValidationDiagnostics.CmtDateModeInvalid,
            $"{subject} has dateMode '{dateMode}'; CMT only accepts {string.Join(", ", CmtDateModes.All.Select(m => $"'{m}'"))}."));
    }

    // The importer ignores <filter>, but the CMT GUI parses it as FetchXML and fails to open a schema with a broken one.
    private static void CheckFilter(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (entity.FetchXmlFilter is null) return;

        string? problem = null;
        try
        {
            var root = XDocument.Parse(entity.FetchXmlFilter).Root;
            if (root is null || root.Name.LocalName != "fetch") problem = $"its root element is <{root?.Name.LocalName}> instead of <fetch>";
        }
        catch (XmlException ex)
        {
            problem = $"it is not well-formed XML ({ex.Message})";
        }

        if (problem is null) return;
        results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtFilterNotFetchXml,
            $"CMT data schema entity '{entity.Name}' has a filter that is not FetchXML: {problem}."));
    }

    private static void CheckPrimaryIdField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryIdField))
        {
            results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data schema entity '{entity.Name}' has no primaryidfield, so imported records cannot be identified."));
            return;
        }

        var field = entity.FindField(entity.PrimaryIdField!);
        string? problem =
            field is null ? "is not declared in <fields>"
            : !string.Equals(field.Type, CmtFieldTypes.Guid, StringComparison.Ordinal) ? $"has type '{field.Type}' instead of 'guid'"
            : null;
        if (problem is not null)
        {
            results.Add(CmtFindings.Error((MetadataBase?)field ?? entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data schema entity '{entity.Name}': primaryidfield '{entity.PrimaryIdField}' {problem}."));
            return;
        }

        // CMT imports identically without the flag; its own generator always writes it.
        if (field!.IsPrimaryKey) return;
        results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
            $"CMT data schema entity '{entity.Name}': primaryidfield '{entity.PrimaryIdField}' is not marked primaryKey=\"true\" as CMT writes it."));
    }

    private static void CheckPrimaryNameField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryNameField) || entity.FindField(entity.PrimaryNameField!) is not null) return;

        results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtPrimaryNameFieldUndeclared,
            $"CMT data schema entity '{entity.Name}': primarynamefield '{entity.PrimaryNameField}' is not declared in <fields>."));
    }

    private static void CheckDuplicateFields(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(entity.Fields, f => f.Name))
        {
            results.Add(CmtFindings.Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data schema entity '{entity.Name}' declares field '{duplicate.Name}' more than once."));
        }
    }

    private static void CheckDuplicateEntities(CmtDataSchema schema, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(schema.Entities, e => e.Name))
        {
            results.Add(CmtFindings.Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data schema declares entity '{duplicate.Name}' more than once."));
        }
    }

    // CMT falls back to the order of <entity> elements when entityImportOrder is absent and ignores names it does not
    // know, so only a present list is checked and every finding is a warning.
    private static void CheckImportOrder(CmtDataSchema schema, List<ValidationResult> results)
    {
        if (schema.EntityImportOrder.Count == 0) return;

        var ordered = new HashSet<string>(schema.EntityImportOrder, StringComparer.Ordinal);
        foreach (var name in schema.EntityImportOrder.Where(n => schema.FindEntity(n) is null))
        {
            var caseMatch = CmtFindings.CaseMatch(schema.Entities.Select(e => e.Name), name);
            if (caseMatch is not null)
            {
                results.Add(CmtFindings.Warning(schema, ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT entityImportOrder names entity '{name}', but the data schema declares it as '{caseMatch}'. CMT compares names case-sensitively and will not find it."));
                continue;
            }

            results.Add(CmtFindings.Warning(schema, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT entityImportOrder names entity '{name}', which the data schema does not declare; CMT ignores it."));
        }

        foreach (var entity in schema.Entities.Where(e => !ordered.Contains(e.Name)))
        {
            results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT data schema entity '{entity.Name}' is missing from entityImportOrder, so its import position is undefined."));
        }
    }

    private static IEnumerable<T> Duplicates<T>(IEnumerable<T> items, Func<T, string> name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(item => !seen.Add(name(item)));
    }
}
