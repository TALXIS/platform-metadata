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

    /// <summary>
    /// Adds a record with the given id. Configuration data should use stable ids committed with the package
    /// (or the TALXIS <c>guidswap</c> extension), never fresh GUIDs per build. Throws when a record with that id exists.
    /// </summary>
    public CmtDataRecord AddRecord(Guid id)
    {
        if (Records.Any(r => r.Id == id)) throw new InvalidOperationException($"A record with id '{id}' already exists on entity '{Name}'.");

        var record = new CmtDataRecord { Id = id };
        Records.Add(record);
        return record;
    }
}
