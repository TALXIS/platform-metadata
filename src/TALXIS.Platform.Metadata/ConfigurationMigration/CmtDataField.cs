namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;field&gt;</c> of a data.xml record: a column value encoded the way CMT expects for the
/// column's schema <see cref="CmtSchemaField.Type"/>.
/// </summary>
public sealed class CmtDataField : MetadataBase
{
    /// <summary>Column logical name; must match a <see cref="CmtSchemaField.Name"/> ordinally or CMT drops the value.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The value in CMT's text encoding for the column's schema type (see docs/configuration-migration.md), or <c>null</c>
    /// when the attribute is absent.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>For filedata columns: the display file name (<c>field@filename</c>); the payload is <c>files/&lt;Value&gt;.bin</c>.</summary>
    public string? FileName { get; set; }

    /// <summary>For lookups: logical name of the referenced table (<c>field@lookupentity</c>).</summary>
    public string? LookupEntity { get; set; }

    /// <summary>For lookups: primary name of the referenced record, used by CMT as a fallback when the id is not found; CMT skips a lookup without it. The TALXIS importer ignores it.</summary>
    public string? LookupEntityName { get; set; }

    /// <summary>
    /// For partylist columns: the activity parties, one <c>&lt;activitypointerrecords&gt;</c> element each, with the party
    /// columns as fields. A party without an id reads as <see cref="Guid.Empty"/> and is written back without one.
    /// </summary>
    public IList<CmtDataRecord> ActivityPointerRecords { get; } = new List<CmtDataRecord>();
}
