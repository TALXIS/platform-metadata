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
    /// The value in CMT's text encoding for the declared type: <c>true|false</c> for bool, invariant numbers
    /// (money may carry a currency symbol), invariant round-trip datetimes, the GUID for lookups and guid
    /// columns, the integer for optionsetvalue, comma-separated integers for optionsetvaluecollection,
    /// base64 for imagedata, the file id for filedata, and HTML-encoded text for string. <c>null</c> when
    /// the attribute is absent (partylist fields carry <see cref="ActivityPointerRecords"/> instead).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>For filedata columns: the display file name (<c>field@filename</c>); the payload is <c>files/&lt;Value&gt;.bin</c>.</summary>
    public string? FileName { get; set; }

    /// <summary>For lookups: logical name of the referenced table (<c>field@lookupentity</c>).</summary>
    public string? LookupEntity { get; set; }

    /// <summary>For lookups: primary name of the referenced record, used by CMT as a fallback when the id is not found. The TALXIS importer ignores it.</summary>
    public string? LookupEntityName { get; set; }

    /// <summary>
    /// For partylist columns: the activity party records nested under <c>&lt;activitypointerrecords&gt;</c>,
    /// each with its own id and fields (typically <c>partyid</c> as a lookup). Empty for every other type.
    /// </summary>
    public IList<CmtDataRecord> ActivityPointerRecords { get; } = new List<CmtDataRecord>();
}
