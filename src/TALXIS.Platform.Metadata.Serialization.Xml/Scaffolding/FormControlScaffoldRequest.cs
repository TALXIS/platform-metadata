namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormControlScaffold"/>: the form to patch, the placement
/// inside it, the rendered control fragments and optional subgrid cell spans.
/// </summary>
public sealed class FormControlScaffoldRequest
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
    /// Rendered control fragment used for entity forms.
    /// </summary>
    public string ControlFilePath { get; set; } = "";

    /// <summary>
    /// Rendered control fragment used when the target is a dialog form.
    /// </summary>
    public string? DialogControlFilePath { get; set; }

    /// <summary>
    /// Subgrid cell row span; set together with <see cref="ColumnSpan"/> for SubGrid controls only.
    /// </summary>
    public string? RowSpan { get; set; }

    /// <summary>
    /// Subgrid cell column span; set together with <see cref="RowSpan"/> for SubGrid controls only.
    /// </summary>
    public string? ColumnSpan { get; set; }
}
