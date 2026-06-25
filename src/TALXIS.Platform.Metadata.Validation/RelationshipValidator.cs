using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Validation;

public sealed class RelationshipValidator
{
    // System columns that back system relationships and live in the platform base,
    // not the solution's Entity.xml, so their absence is never a defect.
    private static readonly HashSet<string> SystemReferencingColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ownerid", "owningbusinessunit", "owninguser", "owningteam", "organizationid",
        "createdby", "createdonbehalfby", "modifiedby", "modifiedonbehalfby",
        "transactioncurrencyid"
    };

    // Attribute types that legitimately back a one-to-many relationship.
    private static readonly HashSet<AttributeType> RelationshipBackingTypes = new()
    {
        AttributeType.Lookup, AttributeType.Customer, AttributeType.Owner,
        AttributeType.File, AttributeType.Image
    };

    // Custom column types an author is expected to pair with a relationship.
    private static readonly HashSet<AttributeType> OrphanCheckTypes = new()
    {
        AttributeType.Lookup, AttributeType.Customer
    };

    public IReadOnlyList<ValidationResult> Validate(Workspace workspace)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));

        var results = new List<ValidationResult>();
        var oneToMany = workspace.Relationships.OfType<OneToManyRelationshipMetadata>().ToList();
        var manyToMany = workspace.Relationships.OfType<ManyToManyRelationshipMetadata>().ToList();
        var shellEntities = BuildShellEntitySet(workspace);

        ValidateRelationshipResolves(oneToMany, results);
        ValidateOneToMany(workspace, oneToMany, shellEntities, results);
        ValidateManyToMany(manyToMany, results);
        ValidateOrphanLookupColumns(workspace, oneToMany, results);

        return results;
    }

    private static HashSet<string> BuildShellEntitySet(Workspace workspace)
    {
        var full = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shell = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var solution in workspace.Solutions)
        {
            foreach (var component in solution.RootComponents)
            {
                if (component.Type != ComponentType.Entity || string.IsNullOrWhiteSpace(component.SchemaName))
                    continue;

                if (component.BehaviorOption == RootComponentBehavior.IncludeSubcomponents)
                    full.Add(component.SchemaName);
                else
                    shell.Add(component.SchemaName);
            }
        }

        shell.ExceptWith(full); 

        return shell;
    }

    private static void ValidateRelationshipResolves(
        IReadOnlyList<OneToManyRelationshipMetadata> relationships,
        List<ValidationResult> results)
    {
        foreach (var relationship in relationships)
        {
            var unresolved =
                string.IsNullOrWhiteSpace(relationship.ReferencingEntity) &&
                string.IsNullOrWhiteSpace(relationship.ReferencingAttribute) &&
                string.IsNullOrWhiteSpace(relationship.ReferencedEntity) &&
                string.IsNullOrWhiteSpace(relationship.ReferencedAttribute);

            if (unresolved)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Relationship '{relationship.SchemaName}' is declared but has no definition " +
                    "(no referencing or referenced entity/column). Its full definition is missing, or the name in " +
                    "Other/Relationships.xml does not match the relationship definition in Other/Relationships/.",
                    null, null, null));
            }
        }
    }

    private static void ValidateOneToMany(
        Workspace workspace,
        IReadOnlyList<OneToManyRelationshipMetadata> relationships,
        HashSet<string> shellEntities,
        List<ValidationResult> results)
    {
        foreach (var relationship in relationships)
        {
            // Referencing (many) side carries the lookup column...
            ValidateColumnExists(workspace, relationship.SchemaName,
                relationship.ReferencingEntity, relationship.ReferencingAttribute,
                requireLookupType: true, shellEntities, results);

            // ...referenced (one) side carries the key it points at.
            ValidateColumnExists(workspace, relationship.SchemaName,
                relationship.ReferencedEntity, relationship.ReferencedAttribute,
                requireLookupType: false, shellEntities, results);
        }
    }

    private static void ValidateColumnExists(
        Workspace workspace,
        string relationshipName,
        string entityName,
        string columnName,
        bool requireLookupType,
        HashSet<string> shellEntities,
        List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(columnName))
            return;

        var entity = workspace.FindEntity(entityName);
        if (entity == null)
            return;

        if (SystemReferencingColumns.Contains(columnName))
            return;

        var attribute = entity.FindAttribute(columnName);
        if (attribute == null)
        {
            var isShell = shellEntities.Contains(entityName);
            results.Add(new ValidationResult(
                isShell ? ValidationSeverity.Warning : ValidationSeverity.Error,
                isShell
                    ? $"Relationship '{relationshipName}' references column '{columnName}' on entity '{entityName}', " +
                      "which is included as a shell in this solution (behavior 1 or 2); the column is likely defined in the solution that owns the entity. Verify it across the full set of solutions."
                    : $"Relationship '{relationshipName}' references column '{columnName}' on entity '{entityName}', " +
                      "but that column is not defined on the entity.",
                null, null, null));
            return;
        }

        if (requireLookupType && !RelationshipBackingTypes.Contains(attribute.AttributeType))
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                $"Relationship '{relationshipName}' references column '{columnName}' on entity '{entityName}', " +
                $"but it is of type '{attribute.AttributeType}', which cannot back a relationship.",
                null, null, null));
        }
    }


    private static void ValidateManyToMany(
        IReadOnlyList<ManyToManyRelationshipMetadata> relationships,
        List<ValidationResult> results)
    {
        foreach (var relationship in relationships)
        {
            if (string.IsNullOrWhiteSpace(relationship.Entity1LogicalName) ||
                string.IsNullOrWhiteSpace(relationship.Entity2LogicalName) ||
                string.IsNullOrWhiteSpace(relationship.IntersectEntityName))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Many-to-many relationship '{relationship.SchemaName}' is missing one of its required names " +
                    "(Entity1LogicalName, Entity2LogicalName, IntersectEntityName).",
                    null, null, null));
            }
        }
    }


    private static void ValidateOrphanLookupColumns(
        Workspace workspace,
        IReadOnlyList<OneToManyRelationshipMetadata> relationships,
        List<ValidationResult> results)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relationship in relationships)
        {
            if (!string.IsNullOrWhiteSpace(relationship.ReferencingEntity) &&
                !string.IsNullOrWhiteSpace(relationship.ReferencingAttribute))
            {
                referenced.Add($"{relationship.ReferencingEntity}|{relationship.ReferencingAttribute}");
            }
        }

        foreach (var entity in workspace.Entities)
        {
            foreach (var attribute in entity.Attributes)
            {
                if (!OrphanCheckTypes.Contains(attribute.AttributeType) || !attribute.IsCustomAttribute)
                    continue;

                if (!referenced.Contains($"{entity.LogicalName}|{attribute.LogicalName}"))
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        $"Lookup column '{attribute.LogicalName}' on entity '{entity.LogicalName}' has no backing one-to-many relationship in this solution. " +
                        "Every lookup column is defined by a relationship; this one's relationship may live in another solution. Verify it across the full set of solutions.",
                        null, null, null));
                }
            }
        }
    }
}
