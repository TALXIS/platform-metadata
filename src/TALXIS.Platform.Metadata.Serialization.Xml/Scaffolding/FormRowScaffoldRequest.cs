namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormRowScaffold"/>: the form to patch, the placement
/// inside it and the rendered row fragment to append.
/// </summary>
public sealed class FormRowScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the entity that owns the form; null lets the locator search.
    /// </summary>
    public string? EntitySchemaName { get; set; }

    /// <summary>
    /// Form type ("main", "quick", "dialog"); null lets the locator search.
    /// </summary>
    public string? FormType { get; set; }

    /// <summary>
    /// GUID of the form to patch, without braces; null lets the locator search.
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// Target tab/column/section inside the form.
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// Rendered row fragment file whose row elements are appended to the section.
    /// </summary>
    public string RowFilePath { get; set; } = "";
}
