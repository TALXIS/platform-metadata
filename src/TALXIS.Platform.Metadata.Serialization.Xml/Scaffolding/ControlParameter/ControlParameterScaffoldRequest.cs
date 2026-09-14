namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="ControlParameterScaffold"/>: the form to patch, the
/// placement of the target control and the rendered parameters fragment.
/// </summary>
public sealed class ControlParameterScaffoldRequest
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
    /// Target tab/column/section/row holding the control's cell.
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// Rendered parameters fragment file; unused placeholder nodes are stripped.
    /// </summary>
    public string ParametersFilePath { get; set; } = "";
}
