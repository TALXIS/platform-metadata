namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtDataEntity : MetadataBase
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public IList<CmtDataRecord> Records { get; set; } = new List<CmtDataRecord>();
    public IList<CmtDataManyToManyRelationship> ManyToManyRelationships { get; set; } = new List<CmtDataManyToManyRelationship>();
}
