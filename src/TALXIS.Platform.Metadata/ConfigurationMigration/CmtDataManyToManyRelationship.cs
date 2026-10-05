namespace TALXIS.Platform.Metadata.ConfigurationMigration;

public sealed class CmtDataManyToManyRelationship : MetadataBase
{
    public Guid SourceId { get; set; }
    public required string TargetEntityName { get; set; }
    public string? TargetEntityNameIdField { get; set; }
    public required string RelationshipName { get; set; }
    public IList<Guid> TargetIds { get; set; } = new List<Guid>();
}
