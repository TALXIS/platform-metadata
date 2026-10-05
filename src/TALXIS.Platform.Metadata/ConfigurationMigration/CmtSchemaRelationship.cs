namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtSchemaRelationship : MetadataBase
{
    public required string Name { get; set; }
    public bool IsManyToMany { get; set; }
    public bool IsReflexive { get; set; }
    public string? RelatedEntityName { get; set; }
    public string? M2mTargetEntity { get; set; }
    public string? M2mTargetEntityPrimaryKey { get; set; }
    public string? ReferencingAttribute { get; set; }
    public string? ReferencedEntity { get; set; }
    public string? ReferencedAttribute { get; set; }
    public string? ReferencingEntity { get; set; }
    public IList<CmtSchemaField> Fields { get; set; } = new List<CmtSchemaField>();
}
