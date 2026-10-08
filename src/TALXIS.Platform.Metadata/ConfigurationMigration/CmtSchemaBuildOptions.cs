namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// Options for <see cref="CmtSchemaBuilder.BuildEntity"/>.
/// </summary>
public sealed class CmtSchemaBuildOptions
{
    /// <summary>
    /// Which columns to include.
    /// </summary>
    public CmtFieldSelection FieldSelection { get; set; } = CmtFieldSelection.Standard;

    /// <summary>
    /// Whether to emit many-to-many relationship entries (on the relationship's Entity1 table, as CMT does), including those whose other table is outside the package (reported as a warning).
    /// </summary>
    public bool IncludeManyToMany { get; set; }
}
