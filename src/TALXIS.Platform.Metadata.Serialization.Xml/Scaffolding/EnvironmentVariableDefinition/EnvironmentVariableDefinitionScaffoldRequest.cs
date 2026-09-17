namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="EnvironmentVariableDefinitionScaffold"/>: the rendered
/// definition file to finalize and register.
/// </summary>
public sealed class EnvironmentVariableDefinitionScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the rendered environmentvariabledefinition.xml.
    /// </summary>
    public string DefinitionFilePath { get; set; } = "";

    /// <summary>
    /// Schema name of the definition (with publisher prefix).
    /// </summary>
    public string SchemaName { get; set; } = "";
}
