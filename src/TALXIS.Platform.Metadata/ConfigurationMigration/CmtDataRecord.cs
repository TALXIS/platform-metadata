namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtDataRecord : MetadataBase
{
    public Guid Id { get; set; }
    public IList<CmtDataField> Fields { get; set; } = new List<CmtDataField>();
}
