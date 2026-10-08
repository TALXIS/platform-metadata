namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// One <c>&lt;relationship&gt;</c> of a data_schema.xml entity. Attribute names are mirrored verbatim: the
/// <c>M2m*</c> members are set for many-to-many entries, the <c>Referenc*</c> members for N:1 entries.
/// </summary>
public sealed class CmtSchemaRelationship : MetadataBase
{
    /// <summary>Relationship name (for M2M the intersect entity name, which data.xml references as <c>m2mrelationshipname</c>).</summary>
    public required string Name { get; set; }

    /// <summary>Whether this is a many-to-many entry (<c>manyToMany="true"</c>). Absent means false.</summary>
    public bool IsManyToMany { get; set; }

    /// <summary>For M2M: whether both sides are the same table (<c>isreflexive</c>). <c>null</c> when absent.</summary>
    public bool? IsReflexive { get; set; }

    /// <summary>For M2M: the related entity name CMT writes (usually equal to <see cref="Name"/>).</summary>
    public string? RelatedEntityName { get; set; }

    /// <summary>For M2M: logical name of the table on the other side.</summary>
    public string? M2mTargetEntity { get; set; }

    /// <summary>For M2M: primary id column of the target table.</summary>
    public string? M2mTargetEntityPrimaryKey { get; set; }

    /// <summary>For N:1: the lookup column on this entity.</summary>
    public string? ReferencingAttribute { get; set; }

    /// <summary>For N:1: the referenced table.</summary>
    public string? ReferencedEntity { get; set; }

    /// <summary>For N:1: the referenced primary id column.</summary>
    public string? ReferencedAttribute { get; set; }

    /// <summary>For N:1: this entity (CMT writes it redundantly).</summary>
    public string? ReferencingEntity { get; set; }

    /// <summary>
    /// For M2M only: the two intersect columns as nested <c>&lt;fields&gt;</c>, carrying name/type/primaryKey/customfield
    /// and no displayname. Empty for N:1 entries.
    /// </summary>
    public IList<CmtSchemaField> Fields { get; } = new List<CmtSchemaField>();

    /// <summary>
    /// Attributes of this element the model does not know (TALXIS importer extensions such as <c>guidswap</c>, or anything newer), by XML
    /// name. They are kept so a package written from these objects, for example a merge, carries them; the writer adds, changes and removes them.
    /// </summary>
    public IDictionary<string, string> OtherAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    // A deep copy, so a merge that adds this relationship to another schema leaves the package it came from unchanged.
    internal CmtSchemaRelationship Copy()
    {
        var copy = new CmtSchemaRelationship
        {
            Name = Name,
            IsManyToMany = IsManyToMany,
            IsReflexive = IsReflexive,
            RelatedEntityName = RelatedEntityName,
            M2mTargetEntity = M2mTargetEntity,
            M2mTargetEntityPrimaryKey = M2mTargetEntityPrimaryKey,
            ReferencingAttribute = ReferencingAttribute,
            ReferencedEntity = ReferencedEntity,
            ReferencedAttribute = ReferencedAttribute,
            ReferencingEntity = ReferencingEntity,
            Source = Source
        };
        foreach (var field in Fields) copy.Fields.Add(field.Copy());
        foreach (var other in OtherAttributes) copy.OtherAttributes[other.Key] = other.Value;
        return copy;
    }
}
