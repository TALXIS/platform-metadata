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

    /// <summary>Display name. Required by CMT's schema but ignored by both importers; TALXIS packages use <c>#</c> as a placeholder.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Entity type code (<c>etc</c>). CMT's schema requires it but its importer only uses it for a batch-mode
    /// capability probe; the TALXIS dialect omits it, hence nullable.
    /// </summary>
    public int? ObjectTypeCode { get; set; }

    /// <summary>Primary id column (<c>primaryidfield</c>); must be a declared <c>guid</c> field marked primaryKey.</summary>
    public string? PrimaryIdField { get; set; }

    /// <summary>Primary name column (<c>primarynamefield</c>); CMT falls back to it when matching by id fails and no updateCompare fields are set.</summary>
    public string? PrimaryNameField { get; set; }

    /// <summary>
    /// When <c>true</c>, CMT bypasses plug-in execution and deactivates the entity's plug-in steps around the
    /// import. Required by CMT's schema (always written by its generator); omitted by the TALXIS dialect, which
    /// ignores it, hence nullable.
    /// </summary>
    public bool? DisablePlugins { get; set; }

    /// <summary>When <c>true</c>, records that already exist are not updated (create-only). <c>null</c> when absent.</summary>
    public bool? SkipUpdate { get; set; }

    /// <summary>When <c>true</c>, CMT skips matching and always creates records. <c>null</c> when absent.</summary>
    public bool? ForceCreate { get; set; }

    /// <summary>
    /// TALXIS extension (not part of Microsoft CMT, which ignores unknown attributes). Source: TALXIS INT0014-DataMovement.
    /// When <c>true</c>, the TALXIS importer renders the whole &lt;records&gt; element of the matching data.xml entity through
    /// DotLiquid (empty variable context; custom tags/filters <c>{% json %}</c>, <c>{% randomguid %}</c>, <c>| json</c>) and re-parses
    /// the result as XML before import. Values may therefore be templates rather than literals until rendered.
    /// Do not flag Liquid syntax in values when this is false: those are runtime templates (e.g. authorization filters), not import-time Liquid.
    /// </summary>
    public bool? RenderLiquid { get; set; }

    /// <summary>
    /// TALXIS extension (not part of Microsoft CMT). When <c>true</c>, the TALXIS importer remaps on import the record id, every
    /// non-lookup field of type <c>guid</c>, M2M source ids, and lookup/M2M target ids that point to entities in the same package
    /// that are also <c>guidswap="true"</c>, using a consistent map (fresh GUIDs, or fixed mappings from an optional guids.json /
    /// request body). References to entities not in the package, or not guidswap, are left unchanged.
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
}
