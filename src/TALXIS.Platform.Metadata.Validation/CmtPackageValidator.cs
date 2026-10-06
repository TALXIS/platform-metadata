using System.Globalization;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Cross-file rules for a CMT package: everything data.xml references must be declared in data_schema.xml,
/// each record's primary-id field must carry its id (TXM017) and each lookup must name its target (TXM021).
/// Names are compared ordinally, as CMT's importer does; a match that only succeeds when letter case is
/// ignored is reported as <see cref="ValidationDiagnostics.CmtNameCaseMismatch"/> instead of "undeclared".
/// </summary>
public sealed class CmtPackageValidator
{
    private static readonly HashSet<string> LookupFieldTypes = new(StringComparer.Ordinal)
    {
        CmtFieldTypes.EntityReference, CmtFieldTypes.Customer, CmtFieldTypes.Owner
    };

    // Lookup targets every CMT export references (owner, createdby, currency) and that exist in every environment.
    private static readonly HashSet<string> SystemLookupTargets = new(StringComparer.Ordinal)
    {
        "systemuser", "team", "businessunit", "transactioncurrency", "organization"
    };

    /// <summary>
    /// Validates the data of a package against its schema. A package without data has nothing to check.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(CmtPackage package)
    {
        var results = new List<ValidationResult>();
        if (package.Data is null) return results;

        CheckTimestamp(package.Data, results);
        foreach (var dataEntity in package.Data.Entities)
        {
            var schemaEntity = package.Schema.FindEntity(dataEntity.Name);
            if (schemaEntity is null)
            {
                var caseMatch = FindEntityIgnoringCase(package.Schema, dataEntity.Name);
                results.Add(caseMatch is null
                    ? CmtFindings.Error(dataEntity, ValidationDiagnostics.CmtDataUndeclared,
                        $"CMT data.xml contains entity '{dataEntity.Name}' ({dataEntity.Records.Count} records), which data_schema.xml does not declare.")
                    : CmtFindings.Error(dataEntity, ValidationDiagnostics.CmtNameCaseMismatch,
                        $"CMT data.xml entity '{dataEntity.Name}' is declared as '{caseMatch.Name}' in data_schema.xml. CMT compares names case-sensitively and fails the import."));
                continue;
            }

            CheckRecordIdentity(schemaEntity, dataEntity, results);
            CheckFields(package.Schema, schemaEntity, dataEntity, results);
            CheckLookups(schemaEntity, dataEntity, results);
            CheckManyToMany(package.Schema, schemaEntity, dataEntity, results);
        }

        return results;
    }

    // CMT creates a record under its primary-id field value; record@id only serves lookups and the second pass. A
    // stable configuration id therefore needs both to agree. One finding per entity and problem.
    private static void CheckRecordIdentity(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        foreach (var duplicate in dataEntity.Records.GroupBy(r => r.Id).Where(g => g.Count() > 1))
        {
            results.Add(CmtFindings.Error(duplicate.ElementAt(1), ValidationDiagnostics.CmtRecordIdentityInvalid,
                $"CMT data.xml entity '{dataEntity.Name}' contains record id '{duplicate.Key}' {duplicate.Count()} times. CMT imports each copy, but lookups to that id are skipped."));
        }

        var idField = schemaEntity.PrimaryIdField;
        if (string.IsNullOrEmpty(idField)) return;

        var problems = dataEntity.Records
            .Select(r => (Record: r, Problem: IdentityProblem(r, idField!)))
            .Where(p => p.Problem is not null)
            .GroupBy(p => p.Problem!.Value);
        foreach (var group in problems)
        {
            var (severity, text) = group.Key;
            var first = group.First().Record;
            results.Add(CmtFindings.Finding(severity, first, ValidationDiagnostics.CmtRecordIdentityInvalid,
                $"CMT data.xml entity '{dataEntity.Name}': {group.Count()} record(s) {text} (first: '{first.Id}')."));
        }
    }

    private static (ValidationSeverity Severity, string Text)? IdentityProblem(CmtDataRecord record, string idField)
    {
        var field = record.Fields.FirstOrDefault(f => string.Equals(f.Name, idField, StringComparison.Ordinal));
        if (field is null) return (ValidationSeverity.Warning, $"have no '{idField}' field, so CMT creates them under a new id");
        // TALXIS Liquid templates are rendered before import.
        if (field.Value is { } value && (value.Contains("{{") || value.Contains("{%"))) return null;
        if (string.IsNullOrEmpty(field.Value)) return (ValidationSeverity.Error, $"have an empty '{idField}' value, so CMT creates them under a new id");
        if (!Guid.TryParse(field.Value, out var id)) return (ValidationSeverity.Error, $"have a '{idField}' value that is not a GUID, so CMT creates them under a new id");
        return id == record.Id ? null : (ValidationSeverity.Error, $"have a '{idField}' value that differs from the record id; CMT creates them under the field value");
    }

    // One finding per entity and field, not per record, so a large package stays readable.
    private static void CheckFields(CmtDataSchema schema, CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var fields = dataEntity.Records.SelectMany(r => r.Fields).ToList();
        var declared = new HashSet<string>(schemaEntity.Fields.Select(f => f.Name), StringComparer.Ordinal);

        foreach (var group in fields.Where(f => !declared.Contains(f.Name)).GroupBy(f => f.Name, StringComparer.Ordinal))
        {
            var caseMatch = schemaEntity.Fields.FirstOrDefault(f => string.Equals(f.Name, group.Key, StringComparison.OrdinalIgnoreCase));
            results.Add(caseMatch is null
                ? CmtFindings.Error(group.First(), ValidationDiagnostics.CmtDataUndeclared,
                    $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({group.Count()} records) is not declared in data_schema.xml. CMT migrates only the fields the schema declares.")
                : CmtFindings.Finding(ValidationSeverity.Warning, group.First(), ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({group.Count()} records) is declared as '{caseMatch.Name}' in data_schema.xml. CMT compares names case-sensitively and will drop the field."));
        }

        var unknownLookups = fields
            .Where(f => !string.IsNullOrEmpty(f.LookupEntity) && schema.FindEntity(f.LookupEntity!) is null && !SystemLookupTargets.Contains(f.LookupEntity!))
            .GroupBy(f => (Field: f.Name, Target: f.LookupEntity!));
        foreach (var group in unknownLookups)
        {
            var first = group.First();
            var caseMatch = FindEntityIgnoringCase(schema, first.LookupEntity!);
            results.Add(caseMatch is null
                ? CmtFindings.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtDataLookupEntityUndeclared,
                    $"CMT data.xml field '{dataEntity.Name}.{first.Name}' points to entity '{first.LookupEntity}' ({group.Count()} records), which the package does not declare. The records must already exist in the target environment.")
                : CmtFindings.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT data.xml field '{dataEntity.Name}.{first.Name}' points to entity '{first.LookupEntity}' ({group.Count()} records), which the package declares as '{caseMatch.Name}'. Dataverse logical names are lowercase; CMT will not resolve the lookup."));
        }
    }

    // CMT resolves a lookup through lookupentity and lookupentityname from data.xml and silently skips it when either is
    // missing or names the wrong table. One finding per entity, field and problem.
    private static void CheckLookups(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var problems = dataEntity.Records.SelectMany(r => r.Fields)
            .Where(f => !string.IsNullOrEmpty(f.Value))
            .Select(f => (Field: f, Schema: schemaEntity.FindField(f.Name)))
            .Where(p => p.Schema is not null && LookupFieldTypes.Contains(p.Schema.Type))
            .Select(p => (p.Field, Problem: LookupProblem(p.Field, p.Schema!)))
            .Where(p => p.Problem is not null)
            .GroupBy(p => (p.Field.Name, p.Problem));
        foreach (var group in problems)
        {
            results.Add(CmtFindings.Finding(ValidationSeverity.Warning, group.First().Field, ValidationDiagnostics.CmtDataLookupIncomplete,
                $"CMT data.xml lookup '{dataEntity.Name}.{group.Key.Name}' ({group.Count()} records) {group.Key.Problem}; CMT skips the lookup without failing the import."));
        }
    }

    private static string? LookupProblem(CmtDataField field, CmtSchemaField schemaField)
    {
        if (string.IsNullOrEmpty(field.LookupEntity)) return "has no lookupentity";
        if (string.IsNullOrEmpty(field.LookupEntityName)) return "has no lookupentityname";
        if (string.IsNullOrEmpty(schemaField.LookupType) || schemaField.LookupType == "*") return null;
        return schemaField.LookupType!.Split('|').Contains(field.LookupEntity, StringComparer.Ordinal)
            ? null
            : $"points to '{field.LookupEntity}', which is not in its lookupType '{schemaField.LookupType}'";
    }

    private static void CheckManyToMany(CmtDataSchema schema, CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var declared = schemaEntity.Relationships.Where(r => r.IsManyToMany).Select(r => r.Name).ToList();
        var declaredSet = new HashSet<string>(declared, StringComparer.Ordinal);

        foreach (var group in dataEntity.ManyToManyRelationships.GroupBy(m => m.RelationshipName, StringComparer.Ordinal))
        {
            var first = group.First();
            if (!declaredSet.Contains(group.Key))
            {
                var caseMatch = declared.FirstOrDefault(n => string.Equals(n, group.Key, StringComparison.OrdinalIgnoreCase));
                results.Add(caseMatch is null
                    ? CmtFindings.Error(first, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                        $"CMT data.xml entity '{dataEntity.Name}' uses many-to-many relationship '{group.Key}', which data_schema.xml does not declare on that entity.")
                    : CmtFindings.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtNameCaseMismatch,
                        $"CMT data.xml entity '{dataEntity.Name}' uses many-to-many relationship '{group.Key}', which data_schema.xml declares as '{caseMatch}'. CMT compares names case-sensitively."));
            }

            // Packages are often split per area, so the target may come from another package already imported.
            if (schema.FindEntity(first.TargetEntityName) is null)
            {
                var caseMatch = FindEntityIgnoringCase(schema, first.TargetEntityName);
                results.Add(caseMatch is null
                    ? CmtFindings.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                        $"CMT data.xml many-to-many relationship '{group.Key}' targets entity '{first.TargetEntityName}', which the package does not declare. The records must already exist in the target environment.")
                    : CmtFindings.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtNameCaseMismatch,
                        $"CMT data.xml many-to-many relationship '{group.Key}' targets entity '{first.TargetEntityName}', which the package declares as '{caseMatch.Name}'. CMT compares names case-sensitively."));
            }
        }
    }

    // CMT parses the timestamp before importing anything; the TALXIS importer ignores it.
    private static void CheckTimestamp(CmtData data, List<ValidationResult> results)
    {
        if (data.Timestamp is null || DateTime.TryParse(data.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)) return;

        results.Add(CmtFindings.Error(data, ValidationDiagnostics.CmtDataTimestampInvalid,
            $"CMT data.xml timestamp '{data.Timestamp}' is not a valid date-time; CMT aborts the import."));
    }

    private static CmtSchemaEntity? FindEntityIgnoringCase(CmtDataSchema schema, string name) =>
        schema.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
