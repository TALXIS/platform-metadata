namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="AppCodeFlowScaffold"/>: the code app project to wire a
/// Power Apps (V2) triggered cloud flow into, and the solution to read it from.
/// </summary>
public sealed class AppCodeFlowScaffoldRequest
{
    /// <summary>
    /// Code app project folder (holds power.config.json, src/, .power/).
    /// </summary>
    public string AppProjectPath { get; set; } = "";

    /// <summary>
    /// Solution project folder that contains the flow, absolute or relative to the code app folder.
    /// </summary>
    public string FlowSolutionPath { get; set; } = "";

    /// <summary>
    /// Flow file name in Workflows without the -GUID suffix (e.g. contoso_myflow).
    /// </summary>
    public string FlowName { get; set; } = "";

    /// <summary>
    /// Rendered index.ts payload used to bootstrap src/generated on first run.
    /// </summary>
    public string IndexTemplateFilePath { get; set; } = "";

    /// <summary>
    /// Rendered CommonModels.ts payload used to bootstrap src/generated on first run.
    /// </summary>
    public string CommonModelsFilePath { get; set; } = "";
}
