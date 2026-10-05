namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtSchemaField : MetadataBase
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsUpdateCompare { get; set; }
    public bool IsCustomField { get; set; }
    public string? LookupType { get; set; }
    public string? DateMode { get; set; }
}
