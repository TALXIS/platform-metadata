namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;m2mrelationship&gt;</c> of data.xml: the targets a source record is associated with through
/// a many-to-many relationship.
/// </summary>
public sealed class CmtDataManyToManyRelationship : MetadataBase
{
    /// <summary>Id of the record on this entity's side of the association.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Logical name of the table on the other side.</summary>
    public required string TargetEntityName { get; set; }

    /// <summary>Primary id column of the target table (<c>targetentitynameidfield</c>).</summary>
    public string? TargetEntityNameIdField { get; set; }

    /// <summary>Relationship name as CMT uses it (<c>m2mrelationshipname</c>, the intersect entity name); must match a schema <see cref="CmtSchemaRelationship.Name"/>.</summary>
    public required string RelationshipName { get; set; }

    /// <summary>Relationship schema name (<c>m2mrelationshipschemaname</c>) written by newer CMT versions; <c>null</c> when absent.</summary>
    public string? RelationshipSchemaName { get; set; }

    /// <summary>Ids of the associated target records, in document order.</summary>
    public IList<Guid> TargetIds { get; } = new List<Guid>();
}
