namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtDataField : MetadataBase
{
    public required string Name { get; set; }
    public string? Value { get; set; }
    public string? LookupEntity { get; set; }
    public string? LookupEntityName { get; set; }
}
