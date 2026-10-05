namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtData : MetadataBase
{
    public string? Timestamp { get; set; }
    public IList<CmtDataEntity> Entities { get; set; } = new List<CmtDataEntity>();

    public CmtDataEntity? FindEntity(string name) =>
        Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
