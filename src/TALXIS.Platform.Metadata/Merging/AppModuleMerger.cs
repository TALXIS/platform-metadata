namespace TALXIS.Platform.Metadata.Merging;

using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

public sealed class AppModuleMerger : IComponentMerger
{
    public ComponentType ComponentType => ComponentType.AppModule;

    public MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers)
    {
        var activeLayers = layers
            .Where(l => l.State != ComponentState.Delete && l.State != ComponentState.UnpublishedDelete && l.Component is AppModuleMetadata)
            .ToList();
        if (activeLayers.Count == 0) return null;

        var topLayer = layers[layers.Count - 1];
        if (topLayer.State == ComponentState.Delete || topLayer.State == ComponentState.UnpublishedDelete)
            return null;

        var baseApp = (AppModuleMetadata)activeLayers[0].Component!;
        var current = baseApp.Body;
        for (int i = 1; i < activeLayers.Count; i++)
        {
            var layerApp = (AppModuleMetadata)activeLayers[i].Component!;
            if (current != null && layerApp.Body != null)
                current = TreeMergeEngine.Merge(current, layerApp.Body);
            else if (layerApp.Body != null)
                current = layerApp.Body;
        }

        var topApp = (AppModuleMetadata)activeLayers[activeLayers.Count - 1].Component!;
        var result = new AppModuleMetadata
        {
            UniqueName = topApp.UniqueName,
            DisplayName = topApp.DisplayName.LocalizedLabels.Count > 0 ? topApp.DisplayName : baseApp.DisplayName,
            IntroducedVersion = topApp.IntroducedVersion ?? baseApp.IntroducedVersion,
            WebResourceId = topApp.WebResourceId ?? baseApp.WebResourceId,
            FormFactor = topApp.FormFactor ?? baseApp.FormFactor,
            ClientType = topApp.ClientType ?? baseApp.ClientType,
            NavigationType = topApp.NavigationType ?? baseApp.NavigationType,
            StateCode = topApp.StateCode ?? baseApp.StateCode,
            StatusCode = topApp.StatusCode ?? baseApp.StatusCode,
            Body = current
        };

        var sourceComponents = topApp.Components.Count > 0 ? topApp.Components : baseApp.Components;
        foreach (var component in sourceComponents)
            result.AddComponent(new AppModuleComponent { Type = component.Type, SchemaName = component.SchemaName, Id = component.Id });

        var sourceRoles = topApp.RoleIds.Count > 0 ? topApp.RoleIds : baseApp.RoleIds;
        foreach (var roleId in sourceRoles)
            result.AddRoleId(roleId);

        return result;
    }
}
