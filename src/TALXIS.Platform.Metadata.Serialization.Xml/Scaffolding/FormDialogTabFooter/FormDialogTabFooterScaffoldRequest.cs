namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormDialogTabFooterScaffold"/>: the dialog form to patch,
/// the tab targeting and the id of the new tab footer.
/// </summary>
public sealed class FormDialogTabFooterScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// GUID of the dialog form to patch, without braces; null picks the newest dialog.
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// Target tab inside the dialog (only tab targeting applies).
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// GUID for the new tab footer, without braces; null generates one.
    /// </summary>
    public string? TabFooterId { get; set; }
}
