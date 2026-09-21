namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="OptionSetGlobalScaffold"/>: the rendered global option
/// set to populate and register.
/// </summary>
public sealed class OptionSetGlobalScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the rendered option set (with publisher prefix).
    /// </summary>
    public string OptionSetName { get; set; } = "";

    /// <summary>
    /// Comma-separated option spec ("{option1},{option2}" or "label:value" pairs).
    /// </summary>
    public string Options { get; set; } = "";
}
