namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormParameterScaffold"/>: the form to patch and the
/// query string parameter to register on it.
/// </summary>
public sealed class FormParameterScaffoldRequest
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
    /// Name attribute of the querystringparameter element.
    /// </summary>
    public string ParameterName { get; set; } = "";

    /// <summary>
    /// Type attribute of the querystringparameter element (e.g. "SafeString").
    /// </summary>
    public string ParameterType { get; set; } = "";
}
