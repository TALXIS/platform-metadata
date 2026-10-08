using TALXIS.Platform.Metadata.Components;
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

    /// <summary>
    /// Validates <paramref name="package"/> against <paramref name="metadata"/>.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(CmtPackage package, Workspace metadata, CmtMetadataValidationOptions? options = null)
    {
        options ??= new CmtMetadataValidationOptions();
        var results = new List<ValidationResult>();

        foreach (var entity in package.Schema.Entities)
        {
            var table = metadata.FindEntity(entity.Name);
            if (table == null)
            {
                results.Add(CmtFindings.Finding(options.UnknownEntitySeverity, entity, ValidationDiagnostics.CmtEntityNotInMetadata,
                    $"CMT data_schema.xml entity '{entity.Name}' is not a table in the metadata. CMT stops the import on a table the target environment does not have."));
                continue;
            }

            ValidateImportable(entity, table, results);
            ValidateFields(entity, table, results);
        }

        return results.Select(r => r with { Stage = ValidationStage.CmtData }).ToList();
    }

    private static void ValidateFields(CmtSchemaEntity entity, EntityMetadata table, List<ValidationResult> results)
    {
        // Letter case is left to CmtDataSchemaValidator's lowercase rule; versionnumber is on every table, solutions just leave it out.
        foreach (var field in entity.Fields.Where(f => f.Name != "versionnumber"))
        {
            var column = table.FindAttribute(field.Name);
            if (column == null)
            {
                results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldNotInMetadata,
                    $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is not a column of table '{table.LogicalName}' in the metadata. CMT rejects the package with 'Missing Fields'."));
                continue;
            }

            ValidateFieldType(entity, field, column, results);
        }
    }

    private static void ValidateFieldType(CmtSchemaEntity entity, CmtSchemaField field, AttributeMetadata column, List<ValidationResult> results)
    {
        var expected = CmtFieldTypeMapper.ToCmtType(column);
        if (expected == null || string.IsNullOrEmpty(field.Type) || ImportedAs(field.Type!) == expected) return;

        results.Add(CmtFindings.Error(field, ValidationDiagnostics.CmtFieldTypeMismatch,
            $"CMT data_schema.xml field '{entity.Name}.{field.Name}' is declared as '{field.Type}', but the column is {column.AttributeType}, which CMT handles as '{expected}'. "
            + "CMT rejects the package with 'Missing Fields'."));
    }

    // The type the importer reads a declared type as: it takes lookup and customer for entityreference, and the TALXIS importer writes file for filedata.
    private static string ImportedAs(string type) => type.ToLowerInvariant() switch
    {
        "lookup" or CmtFieldTypes.Customer => CmtFieldTypes.EntityReference,
        CmtFieldTypes.File => CmtFieldTypes.FileData,
        var lowercase => lowercase
    };

    private static void ValidateImportable(CmtSchemaEntity entity, EntityMetadata table, List<ValidationResult> results)
    {
        if (table.IsImportable != false || ImportableExceptions.Contains(table.LogicalName)) return;

        results.Add(CmtFindings.Error(entity, ValidationDiagnostics.CmtEntityNotInMetadata,
            $"CMT data_schema.xml entity '{entity.Name}' is a table that does not accept data imports. CMT hides such tables and stops the import."));
    }
}
