namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtDataSchema : MetadataBase
{
    public string? DateMode { get; set; }
    public IList<string> EntityImportOrder { get; set; } = new List<string>();
    public IList<CmtSchemaEntity> Entities { get; set; } = new List<CmtSchemaEntity>();

    public CmtSchemaEntity? FindEntity(string name) =>
        Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
