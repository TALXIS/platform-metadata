namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Describes a single component instance for solution-layer import.
/// </summary>
/// <param name="Type">Dataverse component type.</param>
/// <param name="Id">Stable component identifier within the type.</param>
/// <param name="Component">Typed metadata instance for the layer, if one is available.</param>
public sealed record LayerComponentDescriptor(
    ComponentType Type,
    string Id,
    MetadataBase? Component);
