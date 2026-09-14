namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormTabScaffold"/>: the form to patch, the rendered tab
/// fragment, its identity values and the default-tab removal switch.
/// </summary>
public sealed class FormTabScaffoldRequest
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
    /// GUID for the new tab; null generates one (the SetVariables.ps1 behavior).
    /// </summary>
    public string? TabId { get; set; }

    /// <summary>
    /// Display name of the tab; its lowercase alphanumeric form becomes the name attribute.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Remove the scaffolded default tab (name="generaltab") before adding the new one.
    /// </summary>
    public bool RemoveDefaultTab { get; set; }

    /// <summary>
    /// Rendered tab fragment file whose tab elements are appended.
    /// </summary>
    public string TabFilePath { get; set; } = "";
}
