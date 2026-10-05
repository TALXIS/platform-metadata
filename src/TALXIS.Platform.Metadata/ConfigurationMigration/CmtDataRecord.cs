namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;record&gt;</c> of data.xml, identified by the primary key of the record it was exported from.
/// </summary>
public sealed class CmtDataRecord : MetadataBase
{
    /// <summary>Primary key of the record (<c>record@id</c>). CMT matches existing records by it unless the schema's updateCompare fields say otherwise.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Replacement id (<c>record@newId</c>) CMT writes when a record was re-identified during export; the
    /// importer creates the record under this id. <c>null</c> when absent, which is the normal case.
    /// </summary>
    public Guid? NewId { get; set; }

    /// <summary>Field values in document order. Only fields the schema declares are imported.</summary>
    public IList<CmtDataField> Fields { get; } = new List<CmtDataField>();
}
