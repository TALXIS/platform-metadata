namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// The schema side of a Configuration Migration Tool package (data_schema.xml): which tables and columns a
/// package migrates and how records are matched. Both Microsoft CMT and the TALXIS importer read it.
/// </summary>
public sealed class CmtDataSchema : MetadataBase
{
    /// <summary>
    /// How datetime values are shifted on import (<c>entities@dateMode</c>): one of <see cref="CmtDateModes"/>,
    /// or <c>null</c> when absent (absolute). Kept as text so unknown values round-trip and can be reported.
    /// </summary>
    public string? DateMode { get; set; }

    /// <summary>
    /// Entity names in the order CMT should import them (<c>entityImportOrder/entityName</c>). Empty when the
    /// element is absent; CMT then falls back to its built-in order and the data.xml element order.
    /// </summary>
    public IList<string> EntityImportOrder { get; } = new List<string>();

    /// <summary>Declared entities in document order.</summary>
    public IList<CmtSchemaEntity> Entities { get; } = new List<CmtSchemaEntity>();

    /// <summary>Finds an entity by logical name using ordinal comparison (as CMT does), or <c>null</c>.</summary>
    public CmtSchemaEntity? FindEntity(string name) =>
        Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Adds an entity with its <c>guid</c> primary-key field already declared and, when the schema has an
    /// <see cref="EntityImportOrder"/>, appends the name to it. Throws when an entity of that name exists.
    /// </summary>
    /// <returns>The new entity, ready for <see cref="CmtSchemaEntity.AddField"/>.</returns>
    public CmtSchemaEntity AddEntity(string name, string primaryIdField, string primaryNameField, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An entity must have a non-empty name.", nameof(name));
        if (FindEntity(name) != null) throw new InvalidOperationException($"An entity named '{name}' already exists in the data schema.");

        var entity = new CmtSchemaEntity { Name = name, DisplayName = displayName, PrimaryIdField = primaryIdField, PrimaryNameField = primaryNameField };
        entity.Fields.Add(new CmtSchemaField { Name = primaryIdField, DisplayName = displayName ?? name, Type = CmtFieldTypes.Guid, IsPrimaryKey = true });
        Entities.Add(entity);
        if (EntityImportOrder.Count > 0) EntityImportOrder.Add(name);
        return entity;
    }
}
