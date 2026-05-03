namespace TALXIS.Platform.Metadata.Components;

public abstract class AttributeMetadata : MetadataBase
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
}

public enum RequiredLevel
{
    None,
    Recommended,
    Required,
    ApplicationRequired,
    SystemRequired
}
