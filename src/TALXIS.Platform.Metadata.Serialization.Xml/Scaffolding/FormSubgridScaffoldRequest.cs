namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormSubgridScaffold"/>: the form to patch and the rendered
/// subgrid row fragment to append into its rows container.
/// </summary>
public sealed class FormSubgridScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Logical name of the entity that owns the form (e.g. "udpp_warehouseitem").
    /// </summary>
    public string EntityLogicalName { get; set; } = "";

    /// <summary>
    /// FormXml subfolder holding the form file (e.g. "main" or "quick").
    /// </summary>
    public string FormType { get; set; } = "";

    /// <summary>
    /// GUID of the form file to patch, with or without braces.
    /// </summary>
    public string FormId { get; set; } = "";

    /// <summary>
    /// Rendered row fragment file whose row elements are appended to the form.
    /// </summary>
    public string RowFilePath { get; set; } = "";
}
