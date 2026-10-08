using System.Globalization;
using TALXIS.Platform.Metadata.DataMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Cross-file rules for a CMT package: everything data.xml references must be declared in data_schema.xml,
/// each record's primary-id field must carry its id (TXM017), each lookup must name its target (TXM021) and each
/// value must be in the text form CMT reads for its type (TXM022).
/// Names are compared ordinally, as CMT's importer does; a match that only succeeds when letter case is
/// ignored is reported as <see cref="ValidationDiagnostics.CmtNameCaseMismatch"/> instead of "undeclared".
/// </summary>
public sealed class CmtPackageValidator
{
    private static readonly HashSet<string> LookupFieldTypes = new(StringComparer.Ordinal)
    {
        CmtFieldTypes.EntityReference, CmtFieldTypes.Owner
    };

    // Lookup targets every CMT export references (owner, createdby, currency) and that exist in every environment.
    private static readonly HashSet<string> SystemLookupTargets = new(StringComparer.Ordinal)
    {
        "systemuser", "team", "businessunit", "transactioncurrency", "organization"
    };

    /// <summary>Validates the data of a package against its schema; a package without data has nothing to check.</summary>
    /// <param name="package">The package to validate.</param>
    public IReadOnlyList<ValidationResult> Validate(CmtPackage package)
    {
        var results = new List<ValidationResult>();
        if (package.Data == null) return results;

        ValidateTimestamp(package.Data, results);
        foreach (var dataEntity in package.Data.Entities)
        {
            var schemaEntity = package.Schema.FindEntity(dataEntity.Name);
            if (schemaEntity == null)
            {
                var caseMatch = CmtFindings.CaseMatch(package.Schema.Entities.Select(e => e.Name), dataEntity.Name);
                results.Add(caseMatch == null
                    ? CmtFindings.Error(dataEntity, ValidationDiagnostics.CmtDataUndeclared,
                        $"CMT data.xml entity '{dataEntity.Name}' ({Records(dataEntity.Records.Count)}) is not declared in data_schema.xml. CMT skips the entity.")
                    : CmtFindings.Error(dataEntity, ValidationDiagnostics.CmtNameCaseMismatch,
                        $"CMT data.xml entity '{dataEntity.Name}' is declared as '{caseMatch}' in data_schema.xml. CMT compares names case-sensitively and fails the import."));
                continue;
            }

            ValidateRecordIdentity(schemaEntity, dataEntity, results);
            ValidateFields(schemaEntity, dataEntity, results);
            ValidateLookupTargets(package.Schema, dataEntity, results);
            ValidateLookups(schemaEntity, dataEntity, results);
            ValidateValues(schemaEntity, dataEntity, results);
            ValidateMatchKeys(schemaEntity, dataEntity, results);
            ValidateManyToMany(package.Schema, schemaEntity, dataEntity, results);
        }

        return results;
    }

    // CMT parses the timestamp before importing anything.
    private static void ValidateTimestamp(CmtData data, List<ValidationResult> results)
    {
        if (data.Timestamp == null || DateTime.TryParse(data.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)) return;

        results.Add(CmtFindings.Error(data, ValidationDiagnostics.CmtDataTimestampInvalid,
            $"CMT data.xml timestamp '{data.Timestamp}' is not a valid date-time. CMT aborts the import."));
    }

    // CMT creates a record under its primary-id field value; record@id only serves lookups and the second pass. A
    // stable configuration id therefore needs both to agree. One finding per entity and problem.
    private static void ValidateRecordIdentity(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        foreach (var duplicate in dataEntity.Records.GroupBy(r => r.Id).Where(g => g.Count() > 1))
        {
            var differing = DifferingFields(duplicate.ToList());
            var content = differing.Count == 0 ? "identical copies" : $"copies differ in {string.Join(", ", differing)}";
            results.Add(CmtFindings.Error(duplicate.ElementAt(1), ValidationDiagnostics.CmtRecordIdentityInvalid,
                $"CMT data.xml entity '{dataEntity.Name}' has {duplicate.Count()} records with id '{duplicate.Key}' ({content}). CMT imports each copy, but lookups to that id are skipped."));
        }

        var idField = schemaEntity.PrimaryIdField;
        if (string.IsNullOrEmpty(idField)) return;

        var problems = dataEntity.Records
            .Select(r => (Record: r, Problem: IdentityProblem(r, idField!)))
            .Where(p => p.Problem != null)
            .GroupBy(p => p.Problem!.Value);
        foreach (var group in problems)
        {
            var (severity, problem, consequence) = group.Key;
            var first = group.First().Record;
            results.Add(CmtFindings.Finding(severity, first, ValidationDiagnostics.CmtRecordIdentityInvalid,
                $"CMT data.xml entity '{dataEntity.Name}' has {Records(group.Count())} {problem}, starting with '{first.Id}'. {consequence}."));
        }
    }

    private static (ValidationSeverity Severity, string Problem, string Consequence)? IdentityProblem(CmtDataRecord record, string idField)
    {
        var field = record.Fields.FirstOrDefault(f => string.Equals(f.Name, idField, StringComparison.Ordinal));
        if (field == null) return (ValidationSeverity.Warning, $"without a '{idField}' field", "CMT creates them under a new id");
        if (IsTemplate(field.Value)) return null;
        if (string.IsNullOrEmpty(field.Value)) return (ValidationSeverity.Error, $"with an empty '{idField}' value", "CMT creates them under a new id");
        if (!Guid.TryParse(field.Value, out var id)) return (ValidationSeverity.Error, $"with a '{idField}' value that is not a GUID", "CMT creates them under a new id");
        return id == record.Id ? null : (ValidationSeverity.Error, $"with a '{idField}' value that differs from the record id", "CMT creates them under the field value");
    }

    // One finding per entity and field, not per record, so a large package stays readable.
    private static void ValidateFields(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var declared = new HashSet<string>(schemaEntity.Fields.Select(f => f.Name), StringComparer.Ordinal);
        var undeclared = dataEntity.Records.SelectMany(r => r.Fields).Where(f => !declared.Contains(f.Name));

        foreach (var group in undeclared.GroupBy(f => f.Name, StringComparer.Ordinal))
        {
            var caseMatch = CmtFindings.CaseMatch(schemaEntity.Fields.Select(f => f.Name), group.Key);
            results.Add(caseMatch == null
                ? CmtFindings.Error(group.First(), ValidationDiagnostics.CmtDataUndeclared,
                    $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({Records(group.Count())}) is not declared in data_schema.xml. CMT drops the values.")
                : CmtFindings.Warning(group.First(), ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({Records(group.Count())}) is declared as '{caseMatch}' in data_schema.xml. CMT compares names case-sensitively and drops the values."));
        }
    }

    // Lookups may point outside the package, so an unknown target is a warning; a case-only match fails the record.
    private static void ValidateLookupTargets(CmtDataSchema schema, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var unknown = dataEntity.Records.SelectMany(r => r.Fields)
            .Where(f => !string.IsNullOrEmpty(f.LookupEntity) && schema.FindEntity(f.LookupEntity!) == null && !SystemLookupTargets.Contains(f.LookupEntity!));

        foreach (var group in unknown.GroupBy(f => (f.Name, f.LookupEntity)))
        {
            var first = group.First();
            var caseMatch = CmtFindings.CaseMatch(schema.Entities.Select(e => e.Name), first.LookupEntity!);
            results.Add(caseMatch == null
                ? CmtFindings.Warning(first, ValidationDiagnostics.CmtDataLookupEntityUndeclared,
                    $"CMT data.xml field '{dataEntity.Name}.{first.Name}' ({Records(group.Count())}) points to entity '{first.LookupEntity}', which the package does not declare. The records must already exist in the target environment.")
                : CmtFindings.Error(first, ValidationDiagnostics.CmtNameCaseMismatch,
                    $"CMT data.xml field '{dataEntity.Name}.{first.Name}' ({Records(group.Count())}) points to entity '{first.LookupEntity}', which the package declares as '{caseMatch}'. CMT compares names case-sensitively and fails to insert those records."));
        }
    }

    // CMT resolves a lookup through lookupentity and lookupentityname from data.xml and silently skips it when either is
    // missing or names the wrong table. One finding per entity, field and problem.
    private static void ValidateLookups(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var problems = new List<(CmtDataField Field, string Problem)>();
        foreach (var field in dataEntity.Records.SelectMany(r => r.Fields).Where(f => !string.IsNullOrEmpty(f.Value)))
        {
            var schemaField = schemaEntity.FindField(field.Name);
            if (schemaField == null || !LookupFieldTypes.Contains(schemaField.Type)) continue;

            var problem = LookupProblem(field, schemaField);
            if (problem != null) problems.Add((field, problem));
        }

        foreach (var group in problems.GroupBy(p => (p.Field.Name, p.Problem)))
        {
            results.Add(CmtFindings.Warning(group.First().Field, ValidationDiagnostics.CmtDataLookupIncomplete,
                $"CMT data.xml field '{dataEntity.Name}.{group.Key.Name}' ({Records(group.Count())}) {group.Key.Problem}. CMT skips the lookup without failing the import."));
        }
    }

    private static string? LookupProblem(CmtDataField field, CmtSchemaField schemaField)
    {
        if (string.IsNullOrEmpty(field.LookupEntity)) return "has no lookupentity";
        if (string.IsNullOrEmpty(field.LookupEntityName)) return "has no lookupentityname";
        if (string.IsNullOrEmpty(schemaField.LookupType) || schemaField.LookupType == "*") return null;

        // A target that differs only by letter case fails the whole record instead (TXM015).
        if (schemaField.LookupType!.Split('|').Contains(field.LookupEntity, StringComparer.OrdinalIgnoreCase)) return null;
        return $"points to '{field.LookupEntity}', which is not in its lookupType '{schemaField.LookupType}'";
    }

    // CMT parses values per schema type and silently drops, zeroes or misreads what it cannot parse. One finding per
    // entity and field; the primary id is TXM017's.
    private static void ValidateValues(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        // Temporary: tolerates packages for the TALXIS importer; remove when that importer is retired.
        if (schemaEntity.RenderLiquid == true) return;

        var invalid = new List<(CmtDataField Field, string Type)>();
        foreach (var field in dataEntity.Records.SelectMany(r => r.Fields))
        {
            if (string.IsNullOrEmpty(field.Value) || IsTemplate(field.Value)) continue;
            if (string.Equals(field.Name, schemaEntity.PrimaryIdField, StringComparison.Ordinal)) continue;

            var type = schemaEntity.FindField(field.Name)?.Type;
            if (type != null && !CmtValueFormats.IsValid(type, field.Value!)) invalid.Add((field, type));
        }

        foreach (var group in invalid.GroupBy(p => p.Field.Name, StringComparer.Ordinal))
        {
            var (first, type) = group.First();
            results.Add(CmtFindings.Warning(first, ValidationDiagnostics.CmtDataValueInvalid,
                $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({Records(group.Count())}) has a value CMT cannot reliably read as {type}, starting with '{first.Value}'. "
                + ValueConsequence(type, first.Value!)));
        }
    }

    private static string ValueConsequence(string type, string value)
    {
        if (type == CmtFieldTypes.Bool) return "CMT imports it as false.";
        if (CmtValueFormats.HasThousandsSeparator(type, value)) return "It has a thousands separator, so how CMT parses it depends on the importing machine's culture.";
        return "CMT drops or misreads it without failing the import.";
    }

    // CMT matches existing records on the updateCompare fields, or on the primary name when there are none. Records that
    // share those values all match the same existing record on re-import, so it is updated repeatedly and the others never land.
    private static void ValidateMatchKeys(CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        // Temporary: tolerates packages for the TALXIS importer; remove when that importer is retired.
        if (schemaEntity.RenderLiquid == true) return;

        var keyFields = schemaEntity.Fields.Where(f => f.IsUpdateCompare).Select(f => f.Name).ToList();
        if (keyFields.Count == 0 && !string.IsNullOrEmpty(schemaEntity.PrimaryNameField)) keyFields.Add(schemaEntity.PrimaryNameField!);
        if (keyFields.Count == 0) return;

        var keyed = new List<(CmtDataRecord Record, string Key)>();
        foreach (var record in dataEntity.Records)
        {
            var values = keyFields.Select(name => record.Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal))?.Value).ToList();
            if (values.All(string.IsNullOrEmpty)) continue;
            keyed.Add((record, string.Join("', '", values)));
        }

        foreach (var group in keyed.GroupBy(k => k.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            results.Add(CmtFindings.Warning(group.ElementAt(1).Record, ValidationDiagnostics.CmtDataDuplicateMatchKey,
                $"CMT data.xml entity '{dataEntity.Name}' has {group.Count()} records with {string.Join(", ", keyFields)} '{group.Key}'. "
                + "CMT matches records on these values, so on re-import they all update the same existing record. Make the values unique."));
        }
    }

    private static void ValidateManyToMany(CmtDataSchema schema, CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var declared = schemaEntity.Relationships.Where(r => r.IsManyToMany).Select(r => r.Name).ToList();
        var declaredSet = new HashSet<string>(declared, StringComparer.Ordinal);

        foreach (var group in dataEntity.ManyToManyRelationships.GroupBy(m => m.RelationshipName, StringComparer.Ordinal))
        {
            var first = group.First();
            if (!declaredSet.Contains(group.Key))
            {
                var caseMatch = CmtFindings.CaseMatch(declared, group.Key);
                results.Add(caseMatch == null
                    ? CmtFindings.Error(first, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                        $"CMT data.xml many-to-many relationship '{dataEntity.Name}/{group.Key}' is not declared on that entity in data_schema.xml. CMT fails the import after the records are created.")
                    : CmtFindings.Error(first, ValidationDiagnostics.CmtNameCaseMismatch,
                        $"CMT data.xml many-to-many relationship '{dataEntity.Name}/{group.Key}' is declared as '{caseMatch}' in data_schema.xml. CMT compares names case-sensitively and fails the import after the records are created."));
            }

            // Each association names its own target, so check every distinct target and id field, not only the first.
            foreach (var byTarget in group.GroupBy(m => m.TargetEntityName, StringComparer.Ordinal))
            {
                var association = byTarget.First();

                // Packages are often split per area, so the target may come from another package already imported.
                var target = schema.FindEntity(byTarget.Key);
                if (target == null)
                {
                    var caseMatch = CmtFindings.CaseMatch(schema.Entities.Select(e => e.Name), byTarget.Key);
                    results.Add(caseMatch == null
                        ? CmtFindings.Warning(association, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                            $"CMT data.xml many-to-many relationship '{dataEntity.Name}/{group.Key}' targets entity '{byTarget.Key}', which the package does not declare. The records must already exist in the target environment.")
                        : CmtFindings.Warning(association, ValidationDiagnostics.CmtNameCaseMismatch,
                            $"CMT data.xml many-to-many relationship '{dataEntity.Name}/{group.Key}' targets entity '{byTarget.Key}', which the package declares as '{caseMatch}'. CMT compares names case-sensitively and will not find the targets."));
                    continue;
                }

                // CMT reads the target ids through this column and crashes after the records are committed when it is not the target's primary id.
                if (string.IsNullOrEmpty(target.PrimaryIdField)) continue;
                var wrongIdFields = byTarget
                    .Where(m => m.TargetEntityNameIdField != null && !string.Equals(m.TargetEntityNameIdField, target.PrimaryIdField, StringComparison.Ordinal))
                    .GroupBy(m => m.TargetEntityNameIdField, StringComparer.Ordinal)
                    .Select(g => g.First());
                foreach (var wrongIdField in wrongIdFields)
                {
                    results.Add(CmtFindings.Error(wrongIdField, ValidationDiagnostics.CmtDataManyToManyTargetIdFieldInvalid,
                        $"CMT data.xml many-to-many relationship '{dataEntity.Name}/{group.Key}' has targetentitynameidfield '{wrongIdField.TargetEntityNameIdField}', but target entity '{target.Name}' has primaryidfield '{target.PrimaryIdField}'. CMT fails the association."));
                }
            }
        }
    }

    private static string Records(int count) => count == 1 ? "1 record" : $"{count} records";

    // Temporary: tolerates packages for the TALXIS importer; remove when that importer is retired.
    private static bool IsTemplate(string? value) => value != null && (value.Contains("{{") || value.Contains("{%"));

    // Lets the repeated-id finding say what the copies disagree on: the environment keeps the last copy. A field one copy lacks
    // differs; present fields are compared like package merging compares them.
    private static List<string> DifferingFields(IReadOnlyList<CmtDataRecord> records)
    {
        var names = records.SelectMany(r => r.Fields.Select(f => f.Name)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal);
        var differing = names.Where(name => Differs(records, name)).ToList();
        if (records.Select(r => r.NewId).Distinct().Count() > 1) differing.Insert(0, "newId");
        return differing;
    }

    private static bool Differs(IReadOnlyList<CmtDataRecord> records, string name)
    {
        var copies = records.Select(r => r.Fields.FirstOrDefault(f => f.Name == name)).ToList();
        if (copies.Any(f => f == null)) return true;

        return copies.Skip(1).Any(f => !CmtDataComparison.SameContent(copies[0]!, f!));
    }
}
