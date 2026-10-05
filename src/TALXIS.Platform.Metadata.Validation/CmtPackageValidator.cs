using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Cross-file rules for a CMT package: everything data.xml references must be declared in data_schema.xml.
/// </summary>
public sealed class CmtPackageValidator
{
    /// <summary>
    /// Validates the data of a package against its schema. A package without data has nothing to check.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(CmtPackage package)
    {
        var results = new List<ValidationResult>();
        if (package.Data is null) return results;

        foreach (var dataEntity in package.Data.Entities)
        {
            var schemaEntity = package.Schema.FindEntity(dataEntity.Name);
            if (schemaEntity is null)
            {
                results.Add(CmtDataSchemaValidator.Error(dataEntity, ValidationDiagnostics.CmtDataUndeclared,
                    $"CMT data.xml contains entity '{dataEntity.Name}' ({dataEntity.Records.Count} records), which data_schema.xml does not declare."));
                continue;
            }

            CheckFields(package.Schema, schemaEntity, dataEntity, results);
            CheckManyToMany(package.Schema, schemaEntity, dataEntity, results);
        }

        return results;
    }

    // One finding per entity and field, not per record, so a large package stays readable.
    private static void CheckFields(CmtDataSchema schema, CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var fields = dataEntity.Records.SelectMany(r => r.Fields).ToList();

        foreach (var group in fields.Where(f => schemaEntity.FindField(f.Name) is null).GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(CmtDataSchemaValidator.Error(group.First(), ValidationDiagnostics.CmtDataUndeclared,
                $"CMT data.xml field '{dataEntity.Name}.{group.Key}' ({group.Count()} records) is not declared in data_schema.xml. CMT migrates only the fields the schema declares."));
        }

        var unknownLookups = fields
            .Where(f => !string.IsNullOrEmpty(f.LookupEntity) && schema.FindEntity(f.LookupEntity!) is null)
            .GroupBy(f => (Field: f.Name.ToLowerInvariant(), Target: f.LookupEntity!.ToLowerInvariant()));
        foreach (var group in unknownLookups)
        {
            results.Add(CmtDataSchemaValidator.Finding(ValidationSeverity.Warning, group.First(), ValidationDiagnostics.CmtDataLookupEntityUndeclared,
                $"CMT data.xml field '{dataEntity.Name}.{group.First().Name}' points to entity '{group.First().LookupEntity}' ({group.Count()} records), which the package does not declare. The records must already exist in the target environment."));
        }
    }

    private static void CheckManyToMany(CmtDataSchema schema, CmtSchemaEntity schemaEntity, CmtDataEntity dataEntity, List<ValidationResult> results)
    {
        var declared = new HashSet<string>(
            schemaEntity.Relationships.Where(r => r.IsManyToMany).Select(r => r.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in dataEntity.ManyToManyRelationships.GroupBy(m => m.RelationshipName, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (!declared.Contains(group.Key))
            {
                results.Add(CmtDataSchemaValidator.Error(first, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                    $"CMT data.xml entity '{dataEntity.Name}' uses many-to-many relationship '{group.Key}', which data_schema.xml does not declare on that entity."));
            }

            // Packages are often split per area, so the target may come from another package already imported.
            if (schema.FindEntity(first.TargetEntityName) is null)
            {
                results.Add(CmtDataSchemaValidator.Finding(ValidationSeverity.Warning, first, ValidationDiagnostics.CmtDataManyToManyUndeclared,
                    $"CMT data.xml many-to-many relationship '{group.Key}' targets entity '{first.TargetEntityName}', which the package does not declare. The records must already exist in the target environment."));
            }
        }
    }
}
