using System.Globalization;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Rules that check a CMT package against the metadata it will be imported into: do its tables, columns and values exist there.
/// The metadata is a <see cref="Workspace"/>, loaded from solutions offline or filled from a live environment by the caller.
/// </summary>
public sealed class CmtMetadataValidator
{
    // CMT hides tables that are not importable, except these.
    private static readonly HashSet<string> ImportableExceptions = new(StringComparer.Ordinal)
    {
        "activitymimeattachment", "duplicaterule", "duplicaterulecondition", "post", "postcomment", "postfollow", "postlike", "postregarding", "complexcontrol"
    };

    private static readonly (string Suffix, Func<AttributeMetadata, bool> IsBase)[] HelperSuffixes =
    {
        ("_state", c => c.SourceType == AttributeSourceType.Rollup),
        ("_date", c => c.SourceType == AttributeSourceType.Rollup),
        ("yominame", c => c is LookupAttributeMetadata),
        ("name", c => c is LookupAttributeMetadata),
        ("type", c => c is LookupAttributeMetadata)
    };

    /// <summary>
    /// Validates <paramref name="package"/> against <paramref name="metadata"/>.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(CmtPackage package, Workspace metadata, CmtMetadataValidationOptions? options = null)
    {
        options ??= new CmtMetadataValidationOptions();
        var results = new List<ValidationResult>();
        var tables = new CmtMetadataTables(metadata);

        foreach (var entity in package.Schema.Entities)
        {
            var table = tables.Find(entity.Name);
            if (table == null && CmtSystemTables.Contains(entity.Name)) continue;
            if (table == null)
            {
                results.Add(CmtFindings.Finding(options.IncompleteMetadataSeverity, entity, ValidationDiagnostics.CmtEntityNotInMetadata,
                    $"CMT data_schema.xml entity '{entity.Name}' is not a table in the metadata. CMT stops the import if the target environment does not have it."));
                continue;
            }

            // CMT stops at a table it hides, so nothing else about it matters.
            if (!IsImportable(entity, table, results)) continue;

            ValidatePrimaryFields(entity, table, results);
            ValidateFields(entity, table, metadata, options, results);
            ValidateManyToMany(entity, table, metadata, options, results);
            ValidateManyToOne(entity, table, metadata, results);
        }

        foreach (var dataEntity in package.Data?.Entities ?? Enumerable.Empty<CmtDataEntity>())
        {
            // CMT skips data the schema does not declare, and Liquid tables hold templates until the TALXIS importer renders them.
            var entity = package.Schema.FindEntity(dataEntity.Name);
            if (entity == null || entity.RenderLiquid == true) continue;

            ValidateLookupEntities(dataEntity, entity, package.Schema, tables, options, results);

            var table = tables.Find(dataEntity.Name);
            if (table == null) continue;

            ValidateOptionValues(dataEntity, entity, table, metadata, results);
        }

        return results.Select(r => r with { Stage = ValidationStage.CmtData }).ToList();
    }

    private static void ValidatePrimaryFields(CmtSchemaEntity entity, EntityMetadata table, List<ValidationResult> results)
    {
        if (Differs(entity.PrimaryIdField, table.PrimaryIdAttribute, table))
        {
            results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtPrimaryFieldMismatch,
                $"CMT data_schema.xml entity '{entity.Name}' has primaryidfield '{entity.PrimaryIdField}', but the table's primary id column is '{table.PrimaryIdAttribute}'. "
                + "CMT reads record ids from primaryidfield, so the records may not keep their ids and a re-import can create them again."));
        }

        if (Differs(entity.PrimaryNameField, table.PrimaryNameAttribute, table))
        {
            results.Add(CmtFindings.Warning(entity, ValidationDiagnostics.CmtPrimaryFieldMismatch,
                $"CMT data_schema.xml entity '{entity.Name}' has primarynamefield '{entity.PrimaryNameField}', but the table's primary name column is '{table.PrimaryNameAttribute}'."));
        }
    }

    // Missing values on either side are not a mismatch: CmtDataSchemaValidator reports an empty primary field, and the metadata may not say.
    // A primary field that is not a column at all is a typo the missing-field rule already reports with its real consequence.
    private static bool Differs(string? declared, string? inMetadata, EntityMetadata table) =>
        !string.IsNullOrEmpty(declared) && !string.IsNullOrEmpty(inMetadata) && !SameName(declared, inMetadata) && table.FindAttribute(declared!) != null;

    private static void ValidateFields(CmtSchemaEntity entity, EntityMetadata table, Workspace metadata, CmtMetadataValidationOptions options, List<ValidationResult> results)
    {
        var relationships = Relationships(table, metadata);
        var partial = IsPartial(table);
        var unknown = new List<string>();
        // Letter case is left to CmtDataSchemaValidator's lowercase rule; versionnumber is on every table, solutions just leave it out.
        foreach (var field in entity.Fields.Where(f => !SameName(f.Name, "versionnumber")))
        {
            var column = table.FindAttribute(field.Name);
            if (column == null && IsHelperColumn(field.Name, table)) continue;
            if (column == null && partial)
            {
                // A shell lacks its primary id and the system columns by definition, so only the table's own columns are worth naming.
                if (!SameName(field.Name, PrimaryId(table)) && !DataverseSystemColumns.Contains(field.Name)) unknown.Add(field.Name);
                continue;
            }

            if (column == null)
            {
                results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldNotInMetadata,
                    $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is not a column of table '{entity.Name}' in the metadata. CMT rejects the package with 'Missing Fields'."));
                continue;
            }

            // CMT tolerates columns derived from another, the way it tolerates versionnumber.
            if (column.AttributeOf != null) continue;

            ValidateFieldType(entity, field, column, results);
            ValidateLookupTargets(entity, field, column, table, relationships, results);
            ValidateWritable(entity, field, column, results);
        }

        if (unknown.Count == 0) return;

        results.Add(CmtFindings.Finding(options.IncompleteMetadataSeverity, entity, ValidationDiagnostics.CmtFieldNotInMetadata,
            unknown.Count == 1
                ? $"CMT data_schema.xml field '{unknown[0]}' of table '{entity.Name}' is not a column in the metadata. "
                    + "CMT rejects the package with 'Missing Fields' if it is missing in the target environment." + PartialTableNote
                : $"CMT data_schema.xml fields {string.Join(", ", unknown.Select(n => $"'{n}'"))} of table '{entity.Name}' are not columns in the metadata. "
                    + "CMT rejects the package with 'Missing Fields' if they are missing in the target environment." + PartialTableNote));
    }

    // Derived columns Dataverse adds and solutions leave out: a rollup's _state and _date, and a lookup's name, yomi name and type.
    private static bool IsHelperColumn(string name, EntityMetadata table)
    {
        foreach (var (suffix, isBase) in HelperSuffixes)
        {
            if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

            var baseColumn = table.FindAttribute(name.Substring(0, name.Length - suffix.Length));
            if (baseColumn != null && isBase(baseColumn)) return true;
        }

        return false;
    }

    private static void ValidateFieldType(CmtSchemaEntity entity, CmtSchemaField field, AttributeMetadata column, List<ValidationResult> results)
    {
        // A type outside what CMT imports (empty, capitalised, customer, lookup, the TALXIS file) is already reported by CmtDataSchemaValidator.
        var expected = CmtFieldTypeMapper.ToCmtType(column);
        if (expected == null || field.Type == null || !CmtFieldTypes.Importable.Contains(field.Type) || field.Type == expected) return;

        results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeMismatch,
            $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is declared as '{field.Type}', but the column is {Describe(column)}, which CMT handles as '{expected}'. "
            + "CMT rejects the package with 'Missing Fields'."));
    }

    // Owner and customer columns are read as lookups, so their AttributeType alone would say "Lookup" next to a rejected 'lookup'.
    private static string Describe(AttributeMetadata column) => column is LookupAttributeMetadata lookup
        ? lookup.LookupKind switch { LookupKind.Owner => "an owner lookup", LookupKind.Customer => "a customer lookup", _ => "a lookup" }
        : column.AttributeType.ToString();

    // Only a column closed to both create and update is reported: one open to either still takes the value on that path.
    private static void ValidateWritable(CmtSchemaEntity entity, CmtSchemaField field, AttributeMetadata column, List<ValidationResult> results)
    {
        if (ComputedColumns.Contains(column))
        {
            results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldNotWritable,
                $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is a column whose value Dataverse computes itself. "
                + "The import goes on, but its values are dropped or recomputed without an error."));
            return;
        }

        if (column.IsValidForCreate != false || column.IsValidForUpdate != false) return;

        results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtFieldNotWritable,
            $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is a column Dataverse does not let anyone set on create or update. "
            + "The import goes on, but its values are dropped without an error."));
    }

    private static void ValidateLookupTargets(CmtSchemaEntity entity, CmtSchemaField field, AttributeMetadata column, EntityMetadata table,
        IReadOnlyList<RelationshipMetadata> relationships, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(field.LookupType) || field.LookupType == "*") return;

        var allowed = LookupTargets.Resolve(column, table.LogicalName, relationships);
        if (allowed.Count == 0) return;

        // Targets read from the relationships are complete only when the solution holds the whole table; on a shell the owning solution's are missing.
        var complete = (column is LookupAttributeMetadata lookup && lookup.Targets.Any(t => !string.IsNullOrWhiteSpace(t))) || !IsPartial(table);
        var known = string.Join(", ", allowed.Select(a => $"'{a}'"));
        foreach (var target in field.LookupType!.Split('|').Select(t => t.Trim()).Where(t => t.Length > 0 && !allowed.Contains(t.ToLowerInvariant())))
        {
            results.Add(CmtFindings.Warning(field, ValidationDiagnostics.CmtLookupTargetNotAllowed, complete
                ? $"CMT data_schema.xml field '{entity.Name}.{field.Name}' lists lookupType '{target}', but the column can only point to {known}. Dataverse rejects a record whose lookup points there."
                : $"CMT data_schema.xml field '{entity.Name}.{field.Name}' lists lookupType '{target}', but the metadata only knows the column to point to {known}." + PartialTableNote));
        }
    }

    // One finding per field and missing table. A table the package imports itself is already reported as missing from the
    // metadata and will exist once the package runs; the system tables every export points to exist in every environment.
    private static void ValidateLookupEntities(CmtDataEntity dataEntity, CmtSchemaEntity entity, CmtDataSchema schema, CmtMetadataTables tables,
        CmtMetadataValidationOptions options, List<ValidationResult> results)
    {
        var missing = dataEntity.Records.SelectMany(r => r.Fields)
            .Where(f => !string.IsNullOrEmpty(f.LookupEntity) && entity.FindField(f.Name) != null && !CmtSystemTables.Contains(f.LookupEntity!)
                && schema.FindEntity(f.LookupEntity!) == null && tables.Find(f.LookupEntity!) == null)
            .GroupBy(f => (f.Name, f.LookupEntity));
        foreach (var group in missing)
        {
            results.Add(CmtFindings.Finding(options.IncompleteMetadataSeverity, group.First(), ValidationDiagnostics.CmtDataLookupEntityUnknown,
                $"CMT data.xml field '{dataEntity.Name}.{group.Key.Name}' points to table '{group.Key.LookupEntity}' in {group.Count()} record(s), which is not a table in the metadata. "
                + "Those lookups cannot be resolved, so the records fail or lose the lookup."));
        }
    }

    // One finding per field, listing the unknown values; values that are not integers are left to CmtPackageValidator's format rule.
    private static void ValidateOptionValues(CmtDataEntity dataEntity, CmtSchemaEntity entity, EntityMetadata table, Workspace metadata, List<ValidationResult> results)
    {
        var fields = dataEntity.Records.SelectMany(r => r.Fields).Where(f => !string.IsNullOrEmpty(f.Value)).GroupBy(f => f.Name, StringComparer.Ordinal);
        foreach (var group in fields)
        {
            var options = Options(entity.FindField(group.Key), table.FindAttribute(group.Key), metadata);
            if (options == null) continue;

            var unknown = group.Select(f => (field: f, values: OptionValues(f.Value!).Where(v => !options.Contains(v)).ToList())).Where(x => x.values.Count > 0).ToList();
            if (unknown.Count == 0) continue;

            var distinct = unknown.SelectMany(x => x.values).Distinct().OrderBy(v => v).ToList();
            var values = string.Join(", ", distinct.Take(5).Select(v => $"'{v}'")) + (distinct.Count > 5 ? $" and {distinct.Count - 5} more" : string.Empty);
            results.Add(CmtFindings.Warning(unknown[0].field, ValidationDiagnostics.CmtDataOptionValueUnknown,
                $"CMT data.xml field '{dataEntity.Name}.{group.Key}' has option value(s) {values} in {unknown.Count} record(s) that the column's choice does not have in the metadata. "
                + "Dataverse rejects those records."));
        }
    }

    // The option values of a choice column declared as one in the schema, or null when the metadata does not list them.
    private static HashSet<int>? Options(CmtSchemaField? field, AttributeMetadata? column, Workspace metadata)
    {
        if (field == null || column == null) return null;
        if (field.Type is not (CmtFieldTypes.OptionSetValue or CmtFieldTypes.OptionSetValueCollection or CmtFieldTypes.State or CmtFieldTypes.Status)) return null;

        var optionSet = column switch
        {
            PicklistAttributeMetadata picklist => picklist.OptionSet,
            MultiSelectPicklistAttributeMetadata multiSelect => multiSelect.OptionSet,
            _ => null
        };
        if (optionSet == null) return null;

        var listed = optionSet.Options.Count > 0 ? optionSet : metadata.GlobalOptionSets.FirstOrDefault(o => SameName(o.Name, optionSet.Name));
        return listed == null || listed.Options.Count == 0 ? null : new HashSet<int>(listed.Options.Select(o => o.Value));
    }

    // A single value, or a list: CMT's "[-1,1,2,-1]", plain "1,2", or the TALXIS importer's ["1","2"].
    private static IEnumerable<int> OptionValues(string value)
    {
        var trimmed = value.Trim();
        // Only CMT's list form wraps the values in -1 sentinels; elsewhere -1 is a value like any other.
        var sentinels = trimmed.StartsWith("[-1,", StringComparison.Ordinal) && trimmed.EndsWith(",-1]", StringComparison.Ordinal);
        var parts = trimmed.TrimStart('[').TrimEnd(']').Split(',').ToList();
        if (sentinels) parts = parts.Skip(1).Take(parts.Count - 2).ToList();
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim().Trim('"'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var option)) yield return option;
        }
    }

    // CMT finds a many-to-many entry by its intersect table, only among the relationships whose first table (FirstEntityName) is the entity.
    private static void ValidateManyToMany(CmtSchemaEntity entity, EntityMetadata table, Workspace metadata, CmtMetadataValidationOptions options, List<ValidationResult> results)
    {
        var manyToMany = Relationships(table, metadata).OfType<ManyToManyRelationshipMetadata>().ToList();
        foreach (var relationship in entity.Relationships.Where(r => r.IsManyToMany))
        {
            var matches = manyToMany.Where(m => SameName(m.IntersectEntityName, relationship.Name)).ToList();
            if (matches.Any(m => SameName(m.Entity1LogicalName, table.LogicalName))) continue;

            // The relationship's own name often differs from its intersect table's, and it is the one people copy from the solution.
            var named = matches.Count == 0 ? manyToMany.FirstOrDefault(m => SameName(m.SchemaName, relationship.Name)) : null;
            if (named != null)
            {
                results.Add(CmtFindings.Error(relationship, ValidationDiagnostics.CmtManyToManyNotInMetadata,
                    $"CMT data_schema.xml relationship '{entity.Name}.{relationship.Name}' is named after the many-to-many relationship, but CMT finds it by its intersect table, "
                    + $"'{named.IntersectEntityName}'. CMT stops the import."));
                continue;
            }

            // An intersect table that exists makes the entry misplaced rather than unknown, whichever table holds it.
            var found = matches.FirstOrDefault();
            var reason = found == null
                ? "is not a many-to-many relationship in the metadata"
                : $"belongs to table '{Logical(found.Entity1LogicalName)}', the first table (FirstEntityName) of that many-to-many relationship; CMT reads it only there";
            var unknown = found == null && IsPartial(table);
            results.Add(CmtFindings.Finding(unknown ? options.IncompleteMetadataSeverity : ValidationSeverity.Error, relationship, ValidationDiagnostics.CmtManyToManyNotInMetadata,
                $"CMT data_schema.xml relationship '{entity.Name}.{relationship.Name}' {reason}. CMT stops the import." + (unknown ? PartialTableNote : string.Empty)));
        }
    }

    // CMT does not check N:1 entries, so a stale one does not stop the import; it only misleads whoever reads the schema.
    private static void ValidateManyToOne(CmtSchemaEntity entity, EntityMetadata table, Workspace metadata, List<ValidationResult> results)
    {
        var manyToOne = Relationships(table, metadata).OfType<OneToManyRelationshipMetadata>().ToList();
        foreach (var relationship in entity.Relationships.Where(r => !r.IsManyToMany && !string.IsNullOrEmpty(r.Name)))
        {
            var match = manyToOne.FirstOrDefault(m => SameName(m.SchemaName, relationship.Name));
            var problem = ManyToOneProblem(relationship, match, table);
            if (problem == null) continue;

            var note = match == null && IsPartial(table) ? PartialTableNote : string.Empty;
            results.Add(CmtFindings.Warning(relationship, ValidationDiagnostics.CmtRelationshipNotInMetadata,
                $"CMT data_schema.xml relationship '{entity.Name}.{relationship.Name}' {problem}. CMT does not check N:1 entries, so the import goes on, but the entry may be out of date." + note));
        }
    }

    private static string? ManyToOneProblem(CmtSchemaRelationship relationship, OneToManyRelationshipMetadata? match, EntityMetadata table)
    {
        if (match == null) return "is not a relationship in the metadata";
        if (!SameName(match.ReferencingEntity, table.LogicalName)) return $"belongs to table '{Logical(match.ReferencingEntity)}', whose column '{Logical(match.ReferencingAttribute)}' holds the lookup";
        if (SameName(match.ReferencingAttribute, relationship.ReferencingAttribute) && SameName(match.ReferencedEntity, relationship.ReferencedEntity)) return null;
        if (string.IsNullOrEmpty(relationship.ReferencingAttribute) || string.IsNullOrEmpty(relationship.ReferencedEntity))
            return $"does not say which column and table it links; in the metadata it is column '{Logical(match.ReferencingAttribute)}' pointing to '{Logical(match.ReferencedEntity)}'";

        return $"says column '{relationship.ReferencingAttribute}' points to '{relationship.ReferencedEntity}', but in the metadata it is column '{Logical(match.ReferencingAttribute)}' pointing to '{Logical(match.ReferencedEntity)}'";
    }

    private const string PartialTableNote = " The table is only partly in the metadata (its primary id column is missing), so the metadata may just leave this out.";

    // A table added to a solution without all its columns, typically one owned by another solution, lacks its primary id column.
    private static bool IsPartial(EntityMetadata table) => table.FindAttribute(PrimaryId(table)) == null;

    private static string PrimaryId(EntityMetadata table) =>
        !string.IsNullOrEmpty(table.PrimaryIdAttribute) ? table.PrimaryIdAttribute! : table.IsActivity ? "activityid" : $"{table.LogicalName.ToLowerInvariant()}id";

    // Relationships from another solution sit only on the workspace; ones filled in from a live environment may sit only on the table.
    private static IReadOnlyList<RelationshipMetadata> Relationships(EntityMetadata table, Workspace metadata) =>
        metadata.Relationships.Concat(table.Relationships).Distinct().ToList();

    // Metadata from solutions can carry a table's schema name, such as 'Account'; findings use logical names, as the package does.
    private static string Logical(string? name) => (name ?? string.Empty).ToLowerInvariant();

    private static bool SameName(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsImportable(CmtSchemaEntity entity, EntityMetadata table, List<ValidationResult> results)
    {
        if (table.IsImportable != false || ImportableExceptions.Contains(Logical(table.LogicalName))) return true;

        results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtEntityNotInMetadata,
            $"CMT data_schema.xml entity '{entity.Name}' is a table that does not accept data imports. CMT hides such tables and stops the import."));
        return false;
    }
}
