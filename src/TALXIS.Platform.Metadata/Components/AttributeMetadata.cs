namespace TALXIS.Platform.Metadata.Components;

public abstract class AttributeMetadata : MetadataBase, ILocalizedMetadata
{
    public required string LogicalName { get; set; }
    public string? SchemaName { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();
    public abstract AttributeType AttributeType { get; }
    public RequiredLevel RequiredLevel { get; set; } = RequiredLevel.None;
    public bool IsCustomAttribute { get; set; }
    public bool IsAuditEnabled { get; set; }
    public bool IsSearchable { get; set; } = true;
    public bool IsSecured { get; set; }

    /// <summary>
    /// Whether the column can be set on create (<c>ValidForCreateApi</c>); <c>null</c> when the source does not say.
    /// </summary>
    public bool? IsValidForCreate { get; set; }

    /// <summary>
    /// Whether the column can be set on update (<c>ValidForUpdateApi</c>); <c>null</c> when the source does not say.
    /// </summary>
    public bool? IsValidForUpdate { get; set; }

    /// <summary>
    /// Whether the column can be read (<c>ValidForReadApi</c>); <c>null</c> when the source does not say.
    /// </summary>
    public bool? IsValidForRead { get; set; }

    /// <summary>
    /// Where the value comes from; <c>null</c> when the source does not say.
    /// </summary>
    public AttributeSourceType? SourceType { get; set; }
}

public enum RequiredLevel
{
    None,
    Recommended,
    Required,
    ApplicationRequired,
    SystemRequired
}
