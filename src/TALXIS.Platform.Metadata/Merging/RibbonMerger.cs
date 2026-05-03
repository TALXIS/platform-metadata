namespace TALXIS.Platform.Metadata.Merging;

using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

public sealed class RibbonMerger : IComponentMerger
{
    public ComponentType ComponentType => ComponentType.RibbonCustomization;

    public MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers)
    {
        var activeLayers = layers
            .Where(l => l.State != ComponentState.Delete && l.State != ComponentState.UnpublishedDelete && l.Metadata is RibbonMetadata)
            .ToList();
        if (activeLayers.Count == 0) return null;

        var topLayer = layers[layers.Count - 1];
        if (topLayer.State == ComponentState.Delete || topLayer.State == ComponentState.UnpublishedDelete)
            return null;

        var baseRibbon = (RibbonMetadata)activeLayers[0].Metadata!;
        var current = baseRibbon.Body;
        for (int i = 1; i < activeLayers.Count; i++)
        {
            var layerRibbon = (RibbonMetadata)activeLayers[i].Metadata!;
            if (current != null && layerRibbon.Body != null)
                current = TreeMergeEngine.Merge(current, layerRibbon.Body);
            else if (layerRibbon.Body != null)
                current = layerRibbon.Body;
        }

        var topRibbon = (RibbonMetadata)activeLayers[activeLayers.Count - 1].Metadata!;
        return new RibbonMetadata
        {
            EntityLogicalName = topRibbon.EntityLogicalName ?? baseRibbon.EntityLogicalName,
            Body = current
        };
    }
}
