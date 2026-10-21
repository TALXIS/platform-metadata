namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="EntityScaffold"/>: the rendered entity to register.
/// </summary>
public sealed class EntityScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the entity incl. publisher prefix (bare for system entities).
    /// </summary>
    public string EntitySchemaName { get; set; } = "";

    /// <summary>
    /// RootComponent behavior: 0 includes metadata, 2 references an existing entity.
    /// </summary>
    public int Behavior { get; set; }
}
