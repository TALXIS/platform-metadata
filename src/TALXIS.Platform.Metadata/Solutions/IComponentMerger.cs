namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Strategy interface for merging component layers.
/// Implementations handle type-specific merge logic (e.g., form merge, sitemap merge).
/// The merge engine lives in core -- serialization layers only feed it typed objects.
/// </summary>
public interface IComponentMerger
{
    /// <summary>The component type this merger handles.</summary>
    ComponentType ComponentType { get; }

    /// <summary>
    /// Merges all layers in a stack into a single effective component.
    /// Layers are ordered bottom-to-top (base first, active last).
    /// </summary>
    MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers);
}
