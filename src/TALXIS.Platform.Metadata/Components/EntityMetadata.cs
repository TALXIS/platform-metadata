namespace TALXIS.Platform.Metadata.Components;

public sealed class EntityMetadata : MetadataBase
{
    public required string LogicalName { get; set; }
    public string? SchemaName { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label PluralName { get; set; } = new();
    public Label Description { get; set; } = new();
    public OwnershipType Ownership { get; set; } = OwnershipType.UserOwned;
    public string? PrimaryIdAttribute { get; set; }
    public string? PrimaryNameAttribute { get; set; }
    public string? EntitySetName { get; set; }
    public bool IsCustomEntity { get; set; }
    public bool IsActivity { get; set; }
    public bool HasNotes { get; set; }
    public bool HasActivities { get; set; }
    public bool IsAuditEnabled { get; set; }
    public bool ChangeTrackingEnabled { get; set; }
    public bool IsMSTeamsIntegrationEnabled { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalCollectionName { get; set; }
    public string? EntityColor { get; set; }

    private readonly List<AttributeMetadata> _attributes = new();
    public IReadOnlyList<AttributeMetadata> Attributes => _attributes;

    private readonly List<RelationshipMetadata> _relationships = new();
    public IReadOnlyList<RelationshipMetadata> Relationships => _relationships;

    public void AddAttribute(AttributeMetadata attribute) => _attributes.Add(attribute);
    public AttributeMetadata? FindAttribute(string logicalName) =>
        _attributes.FirstOrDefault(a => string.Equals(a.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    public void AddRelationship(RelationshipMetadata relationship) => _relationships.Add(relationship);
}

public enum OwnershipType { None, UserOwned, OrganizationOwned }
