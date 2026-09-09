namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="EntityViewScaffold"/>: the solution root and the entity whose
/// rendered SavedQuery file may need GUID brace normalization.
/// </summary>
public sealed class EntityViewScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the entity the view was rendered under (e.g. "udpp_warehouseitem").
    /// </summary>
    public string EntitySchemaName { get; set; } = "";
}
