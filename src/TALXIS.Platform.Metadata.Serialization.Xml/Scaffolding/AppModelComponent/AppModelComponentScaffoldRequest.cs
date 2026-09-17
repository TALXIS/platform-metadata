namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="AppModelComponentScaffold"/>: the component to register
/// inside a model-driven app's AppModule.xml.
/// </summary>
public sealed class AppModelComponentScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the app's AppModule.xml (the managed variant is probed too).
    /// </summary>
    public string AppModuleFilePath { get; set; } = "";

    /// <summary>
    /// Numeric AppModuleComponent type code ("1" = entity, registered by schema name).
    /// </summary>
    public string ComponentTypeId { get; set; } = "";

    /// <summary>
    /// Schema name of the entity component (type 1).
    /// </summary>
    public string? EntitySchemaName { get; set; }

    /// <summary>
    /// GUID of the component for non-entity types, without braces.
    /// </summary>
    public string? ComponentId { get; set; }
}
