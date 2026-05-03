namespace TALXIS.Platform.Metadata.Merging;

using TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Merges multiple solution layers for a single component into a resolved result.
/// </summary>
public interface IComponentMerger
{
    ComponentType ComponentType { get; }

    /// <summary>
    /// Merges ordered layers (base first, customizations on top) into a single metadata object.
    /// Returns null if the component is deleted by the topmost layer.
    /// </summary>
    MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers);
}
