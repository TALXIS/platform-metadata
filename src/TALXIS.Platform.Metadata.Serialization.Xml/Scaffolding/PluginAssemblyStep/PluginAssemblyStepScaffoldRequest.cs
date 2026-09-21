namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="PluginAssemblyStepScaffold"/>: the rendered SDK message
/// processing step payload to finalize and register.
/// </summary>
public sealed class PluginAssemblyStepScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the rendered step payload file.
    /// </summary>
    public string StepFilePath { get; set; } = "";

    /// <summary>
    /// GUID of the step, without braces.
    /// </summary>
    public string StepId { get; set; } = "";

    /// <summary>
    /// Assembly name whose .dll.data.xml supplies the plugin type id and key token.
    /// </summary>
    public string AssemblyName { get; set; } = "";

    /// <summary>
    /// Plugin class name to look up inside the assembly data XML.
    /// </summary>
    public string PluginClassName { get; set; } = "";

    /// <summary>
    /// Raw filtering attributes spec (e.g. "{a,b}"); empty means none.
    /// </summary>
    public string FilteringAttributes { get; set; } = "";
}
