namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormSectionScaffold"/>: the form to patch, the column
/// targeting, the rendered section fragment and its identity values.
/// </summary>
public sealed class FormSectionScaffoldRequest
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
    /// Target tab/column inside the form.
    /// </summary>
    public FormPlacement Placement { get; set; } = new();

    /// <summary>
    /// GUID for the new section; null generates one.
    /// </summary>
    public string? SectionId { get; set; }

    /// <summary>
    /// Display name of the section; its lowercase alphanumeric form becomes the name attribute.
    /// </summary>
    public string SectionName { get; set; } = "";

    /// <summary>
    /// Rendered section fragment file whose section elements are appended.
    /// </summary>
    public string SectionFilePath { get; set; } = "";
}
