namespace TALXIS.Platform.Metadata.Merging;

using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

public sealed class SiteMapMerger : IComponentMerger
{
    public ComponentType ComponentType => ComponentType.SiteMap;

    public MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers)
    {
        var activeLayers = layers
            .Where(l => l.State != ComponentState.Delete && l.State != ComponentState.UnpublishedDelete && l.Component is SiteMapMetadata)
            .ToList();
        if (activeLayers.Count == 0) return null;

        var topLayer = layers[layers.Count - 1];
        if (topLayer.State == ComponentState.Delete || topLayer.State == ComponentState.UnpublishedDelete)
            return null;

        var baseSiteMap = (SiteMapMetadata)activeLayers[0].Component!;
        var current = baseSiteMap.Body;
        for (int i = 1; i < activeLayers.Count; i++)
        {
            var layerSiteMap = (SiteMapMetadata)activeLayers[i].Component!;
            if (current != null && layerSiteMap.Body != null)
                current = TreeMergeEngine.Merge(current, layerSiteMap.Body);
            else if (layerSiteMap.Body != null)
                current = layerSiteMap.Body;
        }

        var topSiteMap = (SiteMapMetadata)activeLayers[activeLayers.Count - 1].Component!;
        return new SiteMapMetadata
        {
            UniqueName = topSiteMap.UniqueName,
            DisplayName = topSiteMap.DisplayName.LocalizedLabels.Count > 0 ? topSiteMap.DisplayName : baseSiteMap.DisplayName,
            IntroducedVersion = topSiteMap.IntroducedVersion ?? baseSiteMap.IntroducedVersion,
            EnableCollapsibleGroups = topSiteMap.EnableCollapsibleGroups,
            ShowHome = topSiteMap.ShowHome,
            ShowPinned = topSiteMap.ShowPinned,
            ShowRecents = topSiteMap.ShowRecents,
            Body = current
        };
    }
}
