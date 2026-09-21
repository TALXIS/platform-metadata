namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FlowScaffold"/>: the solution to patch and the
/// rendered flow to register in its manifest.
/// </summary>
public sealed class FlowScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// GUID of the rendered flow's workflow, without braces.
    /// </summary>
    public string WorkflowId { get; set; } = "";
}
