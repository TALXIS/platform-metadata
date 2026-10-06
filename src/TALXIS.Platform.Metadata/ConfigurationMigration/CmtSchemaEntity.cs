namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// One <c>&lt;entity&gt;</c> of data_schema.xml: a table to migrate, its columns and relationships.
/// Nullable booleans model an absent attribute (CMT's <c>*Specified</c> semantics): the writer removes the
/// attribute for <c>null</c> and writes <c>true</c>/<c>false</c> otherwise.
/// </summary>
public sealed class CmtSchemaEntity : MetadataBase
{
    /// <summary>Table logical name.</summary>
    public required string Name { get; set; }

    /// <summary>Display name. Written by CMT's generator but ignored by both importers; TALXIS packages use <c>#</c> as a placeholder.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Entity type code (<c>etc</c>). CMT imports and exports without it (its importer only uses it for a batch-mode
    /// capability probe); the TALXIS dialect omits it, hence nullable.
    /// </summary>
    public int? ObjectTypeCode { get; set; }

    /// <summary>Primary id column (<c>primaryidfield</c>); must be a declared <c>guid</c> field, which CMT's generator marks primaryKey.</summary>
    public string? PrimaryIdField { get; set; }

    /// <summary>Primary name column (<c>primarynamefield</c>); CMT matches existing records on it when no updateCompare fields are set.</summary>
    public string? PrimaryNameField { get; set; }

    /// <summary>
    /// When <c>true</c>, CMT bypasses plug-in execution and deactivates the entity's plug-in steps around the
    /// import. Always written by CMT's generator but optional on import; omitted by the TALXIS dialect, which
    /// ignores it, hence nullable.
    /// </summary>
    public bool? DisablePlugins { get; set; }

    /// <summary>When <c>true</c>, records that already exist are not updated (create-only). <c>null</c> when absent.</summary>
    public bool? SkipUpdate { get; set; }

    /// <summary>When <c>true</c>, CMT skips matching and always creates records. <c>null</c> when absent.</summary>
    public bool? ForceCreate { get; set; }

    /// <summary>
    /// TALXIS extension, ignored by Microsoft CMT. When <c>true</c>, the TALXIS importer renders the entity's data.xml
    /// records through Liquid before import, so values may be templates rather than literals.
    /// </summary>
    public bool? RenderLiquid { get; set; }

    /// <summary>
    /// TALXIS extension, ignored by Microsoft CMT. When <c>true</c>, the TALXIS importer replaces the entity's record ids,
    /// and references to them from other <c>guidswap</c> entities in the package, with a consistent set of new ids.
    /// </summary>
    public bool? GuidSwap { get; set; }

    /// <summary>FetchXML used by the CMT GUI to filter the export (<c>&lt;filter&gt;</c> text). Stored but never read by the importer.</summary>
    public string? FetchXmlFilter { get; set; }

    /// <summary>Declared columns in document order.</summary>
    public IList<CmtSchemaField> Fields { get; } = new List<CmtSchemaField>();

    /// <summary>Relationships CMT follows from this entity (N:1 entries and M2M entries emitted from this side).</summary>
    public IList<CmtSchemaRelationship> Relationships { get; } = new List<CmtSchemaRelationship>();

    /// <summary>Finds a field by logical name using ordinal comparison (as CMT does), or <c>null</c>.</summary>
    public CmtSchemaField? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Declares a column. <paramref name="type"/> is a <see cref="CmtFieldTypes"/> value; <paramref name="lookupType"/>
    /// names the target tables of an entityreference column (<c>account|contact</c>). Throws when a field of
    /// that name exists.
    /// </summary>
    public CmtSchemaField AddField(string name, string type, string? displayName = null, bool updateCompare = false, string? lookupType = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A field must have a non-empty name.", nameof(name));
        if (FindField(name) != null) throw new InvalidOperationException($"A field named '{name}' already exists on entity '{Name}'.");

        var field = new CmtSchemaField { Name = name, Type = type, DisplayName = displayName, IsUpdateCompare = updateCompare, LookupType = lookupType };
        Fields.Add(field);
        return field;
    }

    /// <summary>Adds a relationship entry. Throws when one of that name exists.</summary>
    public CmtSchemaRelationship AddRelationship(CmtSchemaRelationship relationship)
    {
        if (Relationships.Any(r => string.Equals(r.Name, relationship.Name, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A relationship named '{relationship.Name}' already exists on entity '{Name}'.");

        Relationships.Add(relationship);
        return relationship;
    }
}
