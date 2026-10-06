using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Structural rules for Configuration Migration Tool data schema files (data_schema.xml): does the schema
/// hang together, and will both Microsoft CMT and the TALXIS importer accept it. The rules need no Dataverse
/// metadata; rules that need data.xml live in <see cref="CmtPackageValidator"/>.
/// </summary>
public sealed class CmtDataSchemaValidator
{
    /// <summary>Validates a file on disk; files that are not CMT data schemas are skipped.</summary>
    /// <param name="filePath">Path to the XML file.</param>
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
    /// <param name="document">The document to validate.</param>
    /// <param name="sourcePath">File path reported on findings.</param>
    public IReadOnlyList<ValidationResult> ValidateXml(XDocument document, string? sourcePath = null)
    {
        var root = document.Root;
        if (root == null || root.Name.LocalName != "entities") return Array.Empty<ValidationResult>();

        // data.xml shares the <entities> root; only schema-form entities declare <fields>.
        var isSchema = root.Elements().Where(e => e.Name.LocalName == "entity").Any(e => e.Elements().Any(c => c.Name.LocalName == "fields"));
        if (!isSchema) return Array.Empty<ValidationResult>();

        return Validate(new CmtPackageXmlReader().ReadSchema(document, sourcePath))
            .Select(r => r.FilePath == null ? r with { FilePath = sourcePath } : r)
            .ToList();
    }

    /// <summary>Validates a CMT data schema model, for example one built in memory before it is saved.</summary>
    /// <param name="schema">The schema to validate.</param>
    public IReadOnlyList<ValidationResult> Validate(CmtDataSchema schema)
    {
        var results = new List<ValidationResult>();

        foreach (var entity in schema.Entities)
        {
            ValidateUpdateCompare(entity, results);
            ValidateLowercaseNames(entity, results);
            ValidatePrimaryIdField(entity, results);
            ValidatePrimaryNameField(entity, results);
            ValidateFieldTypes(entity, results);
            foreach (var field in entity.Fields) ValidateDateMode(field, field.DateMode, $"CMT data_schema.xml field '{entity.Name}.{field.Name}'", results);
            ValidateFilter(entity, results);
            ValidateDuplicateFields(entity, results);
        }

        ValidateDateMode(schema, schema.DateMode, "CMT data_schema.xml root", results);
        ValidateDuplicateEntities(schema, results);
        ValidateImportOrder(schema, results);
        ValidateChildBeforeParent(schema, results);
        return results;
    }

    // Without updateCompare fields CMT matches existing records on the primary name column, across the whole table; without
    // that either, re-imports duplicate every record.
    private static void ValidateUpdateCompare(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (entity.Fields.Any(f => f.IsUpdateCompare)) return;

        results.Add(string.IsNullOrEmpty(entity.PrimaryNameField)
            ? CmtFindings.Error(entity, ValidationDiagnostics.CmtEntityMissingUpdateCompare,
                $"CMT data_schema.xml entity '{entity.Name}' declares no field with updateCompare=\"true\" and no primarynamefield, so CMT cannot match existing records and re-deploys duplicate data.")
            : CmtFindings.Warning(entity, ValidationDiagnostics.CmtEntityMissingUpdateCompare,
                $"CMT data_schema.xml entity '{entity.Name}' declares no field with updateCompare=\"true\". CMT matches existing records on the primary name '{entity.PrimaryNameField}' and overwrites them, including unrelated records with the same name. Mark the fields that identify a record with updateCompare=\"true\"."));
    }

    // Dataverse logical names are lowercase and CMT looks them up case-sensitively: it rejects a package whose schema spells an entity or column otherwise.
    private static void ValidateLowercaseNames(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (!IsLowercase(entity.Name))
        {
            results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtNameCaseMismatch,
                $"CMT data_schema.xml entity '{entity.Name}' is not lowercase. Dataverse logical names are lowercase and CMT compares them case-sensitively, so the import fails."));
        }

        foreach (var field in entity.Fields.Where(f => !IsLowercase(f.Name)))
        {
            results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtNameCaseMismatch,
                $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is not lowercase. Dataverse logical names are lowercase and CMT rejects the package with 'Missing Fields'."));
        }
    }

    private static bool IsLowercase(string name) => string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal);

    private static void ValidatePrimaryIdField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryIdField))
        {
            results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data_schema.xml entity '{entity.Name}' has no primaryidfield, so imported records cannot be identified."));
            return;
        }

        var field = entity.FindField(entity.PrimaryIdField!);
        if (field == null)
        {
            results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data_schema.xml entity '{entity.Name}' has primaryidfield '{entity.PrimaryIdField}', which is not declared in <fields>. CMT cannot update the imported records."));
            return;
        }

        if (!string.Equals(field.Type, CmtFieldTypes.Guid, StringComparison.Ordinal))
        {
            results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
                $"CMT data_schema.xml entity '{entity.Name}' has primaryidfield '{entity.PrimaryIdField}' of type '{field.Type}' instead of 'guid'. CMT cannot update the imported records."));
            return;
        }

        // CMT imports identically without the flag; its own generator always writes it.
        if (field.IsPrimaryKey) return;

        results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtPrimaryIdFieldInvalid,
            $"CMT data_schema.xml entity '{entity.Name}' has primaryidfield '{entity.PrimaryIdField}' without primaryKey=\"true\". Mark it as CMT's generator does."));
    }

    private static void ValidatePrimaryNameField(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(entity.PrimaryNameField) || entity.FindField(entity.PrimaryNameField!) != null) return;

        results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtPrimaryNameFieldUndeclared,
            $"CMT data_schema.xml entity '{entity.Name}' has primarynamefield '{entity.PrimaryNameField}', which is not declared in <fields>. Declare it so CMT can match existing records on it."));
    }

    // CMT's importer looks the type up ordinally in a fixed table; a misspelled or capitalised type and unknown have no conversion.
    private static void ValidateFieldTypes(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        var fields = entity.Fields.Select(f => (Field: f, Owner: entity.Name))
            .Concat(entity.Relationships.SelectMany(r => r.Fields.Select(f => (Field: f, Owner: $"{entity.Name}/{r.Name}"))));

        foreach (var (field, owner) in fields)
        {
            var subject = $"CMT data_schema.xml field '{owner}.{field.Name}'";
            var type = field.Type;
            switch (type)
            {
                case CmtFieldTypes.File:
                    results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                        $"{subject} has type 'file', a TALXIS synonym for 'filedata'. Microsoft CMT rejects the package; use 'filedata' for it."));
                    break;
                case CmtFieldTypes.BigInt:
                    results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                        $"{subject} has type 'bigint'. CMT accepts the schema but drops the values on export and import."));
                    break;
                case CmtFieldTypes.Customer:
                    results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                        $"{subject} has type 'customer', which CMT rejects. Use 'entityreference' with lookupType=\"account|contact\"."));
                    break;
                case CmtFieldTypes.Unknown:
                    results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeNotImportable,
                        $"{subject} has type 'unknown', which CMT exports but cannot import. CMT rejects the package."));
                    break;
                default:
                    if (CmtFieldTypes.Importable.Contains(type)) break;
                    results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeNotImportable, $"{subject} {TypeProblem(type)}. CMT rejects the package."));
                    break;
            }
        }
    }

    private static string TypeProblem(string type)
    {
        if (string.IsNullOrEmpty(type)) return "has no type";
        if (CmtFieldTypes.Importable.Contains(type.ToLowerInvariant())) return $"has type '{type}' instead of '{type.ToLowerInvariant()}', and CMT compares type names case-sensitively";
        return $"has type '{type}', which is not a CMT field type";
    }

    private static void ValidateDateMode(MetadataBase element, string? dateMode, string subject, List<ValidationResult> results)
    {
        if (dateMode == null || CmtDateModes.All.Contains(dateMode)) return;

        results.Add(CmtFindings.Error(element, ValidationDiagnostics.CmtDateModeInvalid,
            $"{subject} has dateMode '{dateMode}', which CMT cannot read. Use {string.Join(", ", CmtDateModes.All.Select(m => $"'{m}'"))}."));
    }

    // The importer ignores <filter>, but the CMT GUI parses it as FetchXML and fails to open a schema with a broken one.
    private static void ValidateFilter(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        if (entity.FetchXmlFilter == null) return;

        string? problem = null;
        try
        {
            var root = XDocument.Parse(entity.FetchXmlFilter).Root;
            if (root == null || root.Name.LocalName != "fetch") problem = $"its root element is <{root?.Name.LocalName}> instead of <fetch>";
        }
        catch (XmlException ex)
        {
            problem = $"it is not well-formed XML ({ex.Message})";
        }

        if (problem == null) return;

        results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtFilterNotFetchXml,
            $"CMT data_schema.xml entity '{entity.Name}' has a filter that is not FetchXML: {problem}. The CMT GUI cannot open the schema; fix or remove the filter."));
    }

    private static void ValidateDuplicateFields(CmtSchemaEntity entity, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(entity.Fields, f => f.Name))
        {
            results.Add(CmtFindings.Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data_schema.xml field '{entity.Name}.{duplicate.Name}' is declared more than once. The TALXIS importer fails on the duplicate."));
        }
    }

    private static void ValidateDuplicateEntities(CmtDataSchema schema, List<ValidationResult> results)
    {
        foreach (var duplicate in Duplicates(schema.Entities, e => e.Name))
        {
            results.Add(CmtFindings.Error(duplicate, ValidationDiagnostics.CmtDuplicateName,
                $"CMT data_schema.xml entity '{duplicate.Name}' is declared more than once. The TALXIS importer fails on the duplicate."));
        }
    }

    // A two-entity cycle (account and contact looking each other up) cannot be ordered at all, so it is not reported.
    private static void ValidateChildBeforeParent(CmtDataSchema schema, List<ValidationResult> results)
    {
        if (schema.EntityImportOrder.Count == 0) return;

        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < schema.EntityImportOrder.Count; i++)
        {
            if (!position.ContainsKey(schema.EntityImportOrder[i])) position[schema.EntityImportOrder[i]] = i;
        }

        foreach (var child in schema.Entities.Where(e => position.ContainsKey(e.Name)))
        {
            foreach (var parent in CmtSchemaBuilder.ReferencedEntities(child).Where(p => position.ContainsKey(p) && position[p] > position[child.Name]))
            {
                var parentEntity = schema.FindEntity(parent);
                if (parentEntity is not null && CmtSchemaBuilder.ReferencedEntities(parentEntity).Contains(child.Name)) continue;

                results.Add(CmtFindings.Warning(child, ValidationDiagnostics.CmtImportOrderChildBeforeParent,
                    $"CMT entityImportOrder imports '{child.Name}' before '{parent}', which it looks up. CMT fills those lookups in its second pass; keep the order only if it is intentional."));
            }
        }
    }

    // CMT falls back to the order of <entity> elements when entityImportOrder is absent and ignores names it does not
    // know, so only a present list is checked and every finding is a warning.
    private static void ValidateImportOrder(CmtDataSchema schema, List<ValidationResult> results)
    {
        if (schema.EntityImportOrder.Count == 0) return;

        var ordered = new HashSet<string>(schema.EntityImportOrder, StringComparer.Ordinal);
        foreach (var name in schema.EntityImportOrder.Where(n => schema.FindEntity(n) == null))
        {
            var caseMatch = CmtFindings.CaseMatch(schema.Entities.Select(e => e.Name), name);
            if (caseMatch != null)
            {
                results.Add(CmtFindings.Warning(schema, ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT data_schema.xml entityImportOrder names entity '{name}', but data_schema.xml declares it as '{caseMatch}'. CMT compares names case-sensitively and will not find it."));
                continue;
            }

            results.Add(CmtFindings.Warning(schema, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT data_schema.xml entityImportOrder names entity '{name}', which data_schema.xml does not declare. CMT ignores it."));
        }

        foreach (var entity in schema.Entities.Where(e => !ordered.Contains(e.Name)))
        {
            results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtImportOrderEntityUndeclared,
                $"CMT data_schema.xml entity '{entity.Name}' is missing from entityImportOrder, so its import position is undefined."));
        }
    }

    // The TALXIS importer keys entities and fields in case-insensitive dictionaries, so case-only duplicates count.
    private static IEnumerable<T> Duplicates<T>(IEnumerable<T> items, Func<T, string> name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(item => !seen.Add(name(item)));
    }
}
