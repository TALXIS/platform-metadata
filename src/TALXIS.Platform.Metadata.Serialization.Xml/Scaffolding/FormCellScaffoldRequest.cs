namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormCellScaffold"/>: the form to patch, the placement
/// inside it and the rendered cell fragments (regular and dialog variants).
/// </summary>
public sealed class FormCellScaffoldRequest
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
    /// Form type ("main", "quick", "dialog"); null auto-detects from the located file.
    /// </summary>
    public string? FormType { get; set; }

    /// <summary>
    /// GUID of the form to patch, without braces; null lets the locator search.
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// Target tab/column/section/row inside the form.
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// Rendered cell fragment used for entity forms.
    /// </summary>
    public string CellFilePath { get; set; } = "";

    /// <summary>
    /// Rendered cell fragment used when the target is a dialog form.
    /// </summary>
    public string? DialogCellFilePath { get; set; }
}
