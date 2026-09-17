namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="EnvironmentVariableValueScaffold"/>: the rendered
/// values stub to fill with the actual value payload.
/// </summary>
public sealed class EnvironmentVariableValueScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the rendered environmentvariablevalues.json stub.
    /// </summary>
    public string ValuesFilePath { get; set; } = "";

    /// <summary>
    /// Raw value of the environment variable, verbatim.
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// GUID of the value record, without braces.
    /// </summary>
    public string ValueId { get; set; } = "";
}
