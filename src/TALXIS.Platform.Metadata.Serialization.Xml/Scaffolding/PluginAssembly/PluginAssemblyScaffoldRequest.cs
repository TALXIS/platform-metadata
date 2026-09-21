namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="PluginAssemblyScaffold"/>: the built plugin project to
/// register in the solution.
/// </summary>
public sealed class PluginAssemblyScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the plugin project folder (holds the csproj), absolute or cwd-relative.
    /// </summary>
    public string PluginProjectRootPath { get; set; } = "";

    /// <summary>
    /// GUID for the PluginAssembly registration, without braces.
    /// </summary>
    public string AssemblyId { get; set; } = "";
}
