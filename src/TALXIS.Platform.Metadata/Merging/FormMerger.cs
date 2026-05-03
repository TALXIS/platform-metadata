namespace TALXIS.Platform.Metadata.Merging;

using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Merges form layers using the tree merge engine.
/// Each layer provides a MergeableNode tree representing its form content.
/// Base layer is the full form; subsequent layers contain only diffs with MergeActions.
/// </summary>
public sealed class FormMerger : IComponentMerger
{
    public ComponentType ComponentType => ComponentType.SystemForm;

    public MetadataBase? Merge(IReadOnlyList<ComponentLayer> layers)
    {
        if (layers.Count == 0)
            return null;

        var baseLayer = layers[0];
        if (baseLayer.Component is not FormMetadata baseForm || baseForm.Body == null)
            return baseLayer.Component;

        var current = baseForm.Body;

        for (int i = 1; i < layers.Count; i++)
        {
            if (layers[i].Component is FormMetadata layerForm && layerForm.Body != null)
            {
                current = TreeMergeEngine.Merge(current, layerForm.Body);
            }
        }

        // Build result form from the base metadata with merged body
        var result = new FormMetadata
        {
            FormId = baseForm.FormId,
            FormType = baseForm.FormType,
            Name = baseForm.Name,
            IntroducedVersion = baseForm.IntroducedVersion,
            FormPresentation = baseForm.FormPresentation,
            FormActivationState = baseForm.FormActivationState,
            IsCustomizable = baseForm.IsCustomizable,
            CanBeDeleted = baseForm.CanBeDeleted,
            EntityLogicalName = baseForm.EntityLogicalName,
            Body = current
        };

        // Copy labels
        foreach (var kvp in baseForm.DisplayName.LocalizedLabels)
            result.DisplayName[kvp.Key] = kvp.Value;
        foreach (var kvp in baseForm.Description.LocalizedLabels)
            result.Description[kvp.Key] = kvp.Value;

        return result;
    }
}
