namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// The records side of a Configuration Migration Tool package (data.xml): per entity the exported
/// records and many-to-many associations. Entity and field names refer to <see cref="CmtDataSchema"/>
/// declarations by ordinal comparison, as CMT's importer does.
/// </summary>
public sealed class CmtData : MetadataBase
{
    /// <summary>
    /// Export timestamp exactly as written in <c>entities@timestamp</c> (CMT writes an invariant
    /// round-trip DateTime such as <c>2026-01-15T10:00:00.0000000Z</c>). Kept as text so unparseable
    /// values round-trip and can be reported; <c>null</c> when absent.
    /// </summary>
    public string? Timestamp { get; set; }

    /// <summary>Entities in document order. CMT imports entities it finds no import-order entry for in this order.</summary>
    public IList<CmtDataEntity> Entities { get; } = new List<CmtDataEntity>();

    /// <summary>Finds an entity by logical name using ordinal comparison, or <c>null</c>.</summary>
    public CmtDataEntity? FindEntity(string name) =>
        Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
}
