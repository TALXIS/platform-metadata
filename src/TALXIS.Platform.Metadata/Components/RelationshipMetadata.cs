namespace TALXIS.Platform.Metadata.Components;

public abstract class RelationshipMetadata : MetadataBase
{
    public required string SchemaName { get; set; }
    public bool IsCustomRelationship { get; set; }
    public bool IsCustomizable { get; set; }
    public string? IntroducedVersion { get; set; }
    public Label Description { get; set; } = new();

    private readonly List<RelationshipRoleMetadata> _roles = new();
    public IReadOnlyList<RelationshipRoleMetadata> Roles => _roles;

    public void AddRole(RelationshipRoleMetadata role) => _roles.Add(role);
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
    public CascadeType CascadeArchive { get; set; } = CascadeType.RemoveLink;
    public CascadeType CascadeRollupView { get; set; } = CascadeType.NoCascade;
    public bool IsHierarchical { get; set; }
    public bool IsValidForAdvancedFind { get; set; }
}

public sealed class ManyToManyRelationshipMetadata : RelationshipMetadata
{
    public required string Entity1LogicalName { get; set; }
    public required string Entity2LogicalName { get; set; }
    public required string IntersectEntityName { get; set; }
}

public sealed class RelationshipRoleMetadata
{
    public string? NavPaneDisplayOption { get; set; }
    public string? NavPaneArea { get; set; }
    public int? NavPaneOrder { get; set; }
    public string? NavigationPropertyName { get; set; }
    public int? RelationshipRoleType { get; set; }
}

public enum CascadeType { NoCascade, Cascade, Active, UserOwned, RemoveLink, Restrict }
