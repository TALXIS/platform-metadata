namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;record&gt;</c> of data.xml, identified by the primary key of the record it was exported from.
/// </summary>
public sealed class CmtDataRecord : MetadataBase
{
    /// <summary>
    /// Primary key of the record (<c>record@id</c>), used for lookups and associations within the package. CMT creates the record
    /// under its primary-id field value, so that field must carry the same GUID (TXM017).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Replacement id (<c>record@newId</c>) CMT writes when a record was re-identified during export; the
    /// importer creates the record under this id. <c>null</c> when absent, which is the normal case.
    /// </summary>
    public Guid? NewId { get; set; }

    /// <summary>Field values in document order. Only fields the schema declares are imported.</summary>
    public IList<CmtDataField> Fields { get; } = new List<CmtDataField>();

    /// <summary>
    /// Attributes of this element the model does not know (TALXIS importer extensions such as <c>guidswap</c>, or anything newer), by XML
    /// name. They are kept so a package written from these objects, for example a merge, carries them; the writer adds, changes and removes them.
    /// </summary>
    public IDictionary<string, string> OtherAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Sets a field value, replacing an existing field of the same name (ordinal) or appending a new one.
    /// <paramref name="value"/> must already be in CMT's encoding for the column type (see <see cref="CmtDataField.Value"/>).
    /// </summary>
    /// <returns>This record, so calls chain.</returns>
    public CmtDataRecord Set(string name, string? value, string? lookupEntity = null, string? lookupEntityName = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A field must have a non-empty name.", nameof(name));

        var field = Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
        if (field == null)
        {
            field = new CmtDataField { Name = name };
            Fields.Add(field);
        }

        field.Value = value;
        field.LookupEntity = lookupEntity;
        field.LookupEntityName = lookupEntityName;
        return this;
    }
}
