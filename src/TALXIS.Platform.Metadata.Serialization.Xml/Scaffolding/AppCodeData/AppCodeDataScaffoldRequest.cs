namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="AppCodeDataScaffold"/>: the code app project to wire a
/// Dataverse entity data source into, and the model solution to read it from.
/// </summary>
public sealed class AppCodeDataScaffoldRequest
{
    /// <summary>
    /// Code app project folder (holds power.config.json, src/, .power/).
    /// </summary>
    public string AppProjectPath { get; set; } = "";

    /// <summary>
    /// Logical name of the entity including publisher prefix (e.g. udpp_warehouseitem).
    /// </summary>
    public string EntityLogicalName { get; set; } = "";

    /// <summary>
    /// Metadata root of the model solution (folder containing Entities/, OptionSets/).
    /// </summary>
    public string ModelSolutionRootPath { get; set; } = "";

    /// <summary>
    /// Rendered service file still carrying the name placeholders.
    /// </summary>
    public string ServiceFilePath { get; set; } = "";

    /// <summary>
    /// Rendered index.ts payload used to bootstrap src/generated on first run.
    /// </summary>
    public string IndexTemplateFilePath { get; set; } = "";

    /// <summary>
    /// Rendered CommonModels.ts payload used to bootstrap src/generated on first run.
    /// </summary>
    public string CommonModelsFilePath { get; set; } = "";
}
