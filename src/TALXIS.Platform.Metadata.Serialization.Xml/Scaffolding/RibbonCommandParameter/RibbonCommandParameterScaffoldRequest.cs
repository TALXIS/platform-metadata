namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="RibbonCommandParameterScaffold"/>: the entity ribbon
/// to patch and the command function the rendered parameters go to.
/// </summary>
public sealed class RibbonCommandParameterScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the entity's RibbonDiff.xml.
    /// </summary>
    public string RibbonDiffFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered parameters payload.
    /// </summary>
    public string ParametersFilePath { get; set; } = "";

    /// <summary>
    /// Full command definition id (prefix.entity.Command.buttonlogicalname).
    /// </summary>
    public string CommandDefinitionId { get; set; } = "";

    /// <summary>
    /// FunctionName of the JavaScriptFunction the parameters are appended to.
    /// </summary>
    public string FunctionName { get; set; } = "";
}
