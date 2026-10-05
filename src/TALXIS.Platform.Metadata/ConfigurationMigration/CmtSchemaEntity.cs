namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtSchemaEntity : MetadataBase
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public int? ObjectTypeCode { get; set; }
    public string? PrimaryIdField { get; set; }
    public string? PrimaryNameField { get; set; }
    public bool DisablePlugins { get; set; }
    public bool SkipUpdate { get; set; }
    public bool ForceCreate { get; set; }
    public string? FetchXmlFilter { get; set; }
    public IList<CmtSchemaField> Fields { get; set; } = new List<CmtSchemaField>();
    public IList<CmtSchemaRelationship> Relationships { get; set; } = new List<CmtSchemaRelationship>();

    public CmtSchemaField? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}
