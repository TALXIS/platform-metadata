namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// In-memory representation of a Dataverse table definition.
/// </summary>
public sealed class EntityMetadata : MetadataBase, ILocalizedMetadata
{
    /// <summary>
    /// Gets or sets the logical table name.
    /// </summary>
    public required string LogicalName { get; set; }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <inheritdoc />
    public Label DisplayName { get; set; } = new();

    /// <summary>
    /// Gets or sets the localized plural display name.
    /// </summary>
    public Label PluralName { get; set; } = new();

    /// <inheritdoc />
    public Label Description { get; set; } = new();

    /// <summary>
    /// Gets or sets the table ownership model.
    /// </summary>
    public OwnershipType Ownership { get; set; } = OwnershipType.UserOwned;

    /// <summary>
    /// Gets or sets the primary key attribute logical name.
    /// </summary>
    public string? PrimaryIdAttribute { get; set; }

    /// <summary>
    /// Gets or sets the primary name attribute logical name.
    /// </summary>
    public string? PrimaryNameAttribute { get; set; }

    /// <summary>
    /// Gets or sets the OData entity-set name.
    /// </summary>
    public string? EntitySetName { get; set; }

    /// <summary>
    /// Gets or sets whether the table is custom.
    /// </summary>
    public bool IsCustomEntity { get; set; }

    /// <summary>
    /// Gets or sets whether the table is an activity type.
    /// </summary>
    public bool IsActivity { get; set; }

    /// <summary>
    /// Gets or sets whether notes are enabled.
    /// </summary>
    public bool HasNotes { get; set; }

    /// <summary>
    /// Gets or sets whether activities are enabled.
    /// </summary>
    public bool HasActivities { get; set; }

    /// <summary>
    /// Gets or sets whether auditing is enabled.
    /// </summary>
    public bool IsAuditEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether change tracking is enabled.
    /// </summary>
    public bool ChangeTrackingEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether Microsoft Teams integration is enabled.
    /// </summary>
    public bool IsMSTeamsIntegrationEnabled { get; set; }

    /// <summary>
    /// Gets or sets the external singular name, if configured.
    /// </summary>
    public string? ExternalName { get; set; }

    /// <summary>
    /// Gets or sets the external plural name, if configured.
    /// </summary>
    public string? ExternalCollectionName { get; set; }

    /// <summary>
    /// Gets or sets the configured table color.
    /// </summary>
    public string? EntityColor { get; set; }

    private readonly List<AttributeMetadata> _attributes = new();

    /// <summary>
    /// Gets the attributes that belong to the table.
    /// </summary>
    public IReadOnlyList<AttributeMetadata> Attributes => _attributes;

    private readonly List<RelationshipMetadata> _relationships = new();

    /// <summary>
    /// Gets the relationships declared for the table.
    /// </summary>
    public IReadOnlyList<RelationshipMetadata> Relationships => _relationships;

    /// <summary>
    /// Adds an attribute to the table.
    /// </summary>
    /// <param name="attribute">Attribute metadata to add.</param>
    public void AddAttribute(AttributeMetadata attribute) => _attributes.Add(attribute);

    /// <summary>
    /// Finds an attribute by logical name using case-insensitive comparison.
    /// </summary>
    /// <param name="logicalName">Attribute logical name.</param>
    /// <returns>The matching attribute, or <see langword="null"/> when not found.</returns>
    public AttributeMetadata? FindAttribute(string logicalName) =>
        _attributes.FirstOrDefault(a => string.Equals(a.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes an attribute by logical name using case-insensitive comparison.
    /// </summary>
    /// <param name="logicalName">Attribute logical name.</param>
    /// <returns><see langword="true"/> when a matching attribute was removed.</returns>
    public bool RemoveAttribute(string logicalName) =>
        _attributes.RemoveAll(a => string.Equals(a.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>
    /// Adds a relationship to the table.
    /// </summary>
    /// <param name="relationship">Relationship metadata to add.</param>
    public void AddRelationship(RelationshipMetadata relationship) => _relationships.Add(relationship);
}

/// <summary>
/// Ownership model used by a Dataverse table.
/// </summary>
public enum OwnershipType
{
    /// <summary>
    /// Ownership is unspecified.
    /// </summary>
    None,

    /// <summary>
    /// Records are owned by users or teams.
    /// </summary>
    UserOwned,

    /// <summary>
    /// Records are owned by the organization.
    /// </summary>
    OrganizationOwned
}
