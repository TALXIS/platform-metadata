namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;entity&gt;</c> of data.xml: the exported records of a table and its many-to-many associations.
/// </summary>
public sealed class CmtDataEntity : MetadataBase
{
    /// <summary>Table logical name; must match a <see cref="CmtSchemaEntity.Name"/> ordinally or CMT skips the entity.</summary>
    public required string Name { get; set; }

    /// <summary>Display name as exported; informational only.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Records in document order.</summary>
    public IList<CmtDataRecord> Records { get; } = new List<CmtDataRecord>();

    /// <summary>Many-to-many associations whose source record belongs to this entity.</summary>
    public IList<CmtDataManyToManyRelationship> ManyToManyRelationships { get; } = new List<CmtDataManyToManyRelationship>();
}
