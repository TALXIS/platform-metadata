namespace TALXIS.Platform.Metadata.Components;

public abstract class RelationshipMetadata : MetadataBase
{
    public required string SchemaName { get; set; }
    public bool IsCustomRelationship { get; set; }
}

public sealed class OneToManyRelationshipMetadata : RelationshipMetadata
{
    public required string ReferencedEntity { get; set; }
    public required string ReferencedAttribute { get; set; }
    public required string ReferencingEntity { get; set; }
    public required string ReferencingAttribute { get; set; }
    public CascadeType CascadeDelete { get; set; } = CascadeType.RemoveLink;
    public CascadeType CascadeAssign { get; set; } = CascadeType.NoCascade;
    public CascadeType CascadeMerge { get; set; } = CascadeType.NoCascade;
    public CascadeType CascadeReparent { get; set; } = CascadeType.NoCascade;
    public CascadeType CascadeShare { get; set; } = CascadeType.NoCascade;
    public CascadeType CascadeUnshare { get; set; } = CascadeType.NoCascade;
}

public sealed class ManyToManyRelationshipMetadata : RelationshipMetadata
{
    public required string Entity1LogicalName { get; set; }
    public required string Entity2LogicalName { get; set; }
    public required string IntersectEntityName { get; set; }
}

public enum CascadeType { NoCascade, Cascade, Active, UserOwned, RemoveLink, Restrict }
