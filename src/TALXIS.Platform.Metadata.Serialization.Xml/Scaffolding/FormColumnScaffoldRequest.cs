namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormColumnScaffold"/>: the form to patch, the tab (or
/// footer) targeting and the rendered column fragment to append.
/// </summary>
public sealed class FormColumnScaffoldRequest
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
    /// Target tab inside the form (only tab and footer targeting applies).
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// Rendered column fragment file whose column elements are appended.
    /// </summary>
    public string ColumnFilePath { get; set; } = "";
}
