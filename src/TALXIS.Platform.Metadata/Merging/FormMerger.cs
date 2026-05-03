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

        // If the topmost layer marks the component as deleted, return null
        var topLayer = layers[layers.Count - 1];
        if (topLayer.State == ComponentState.Delete || topLayer.State == ComponentState.UnpublishedDelete)
            return null;

        // Filter out deleted layers, keep Published and Unpublished
        var activeLayers = layers
            .Where(l => l.State != ComponentState.Delete && l.State != ComponentState.UnpublishedDelete && l.Metadata is FormMetadata)
            .ToList();

        if (activeLayers.Count == 0)
            return null;

        var baseLayer = activeLayers[0];
        if (baseLayer.Metadata is not FormMetadata baseForm || baseForm.Body == null)
            return baseLayer.Metadata;

        var current = baseForm.Body;

        for (int i = 1; i < activeLayers.Count; i++)
        {
            if (activeLayers[i].Metadata is FormMetadata layerForm && layerForm.Body != null)
            {
                current = TreeMergeEngine.Merge(current, layerForm.Body);
            }
        }

        // Build result form from the base metadata with merged body
        var result = new FormMetadata
        {
            FormId = baseForm.FormId,
            FormType = baseForm.FormType,
            IntroducedVersion = baseForm.IntroducedVersion,
            FormPresentation = baseForm.FormPresentation,
            FormActivationState = baseForm.FormActivationState,
            IsCustomizable = baseForm.IsCustomizable,
            CanBeDeleted = baseForm.CanBeDeleted,
            EntityLogicalName = baseForm.EntityLogicalName,
            Body = current
        };

        // Copy labels from base
        foreach (var kvp in baseForm.DisplayName.LocalizedLabels)
            result.DisplayName[kvp.Key] = kvp.Value;
        foreach (var kvp in baseForm.Description.LocalizedLabels)
            result.Description[kvp.Key] = kvp.Value;

        // Apply top-wins for scalar properties from topmost layer
        var topForm = activeLayers[activeLayers.Count - 1].Metadata as FormMetadata;
        if (topForm != null && activeLayers.Count > 1)
        {
            result.FormType = topForm.FormType ?? result.FormType;
            result.IsCustomizable = topForm.IsCustomizable;
            result.CanBeDeleted = topForm.CanBeDeleted;
            result.FormPresentation = topForm.FormPresentation ?? result.FormPresentation;
            result.FormActivationState = topForm.FormActivationState ?? result.FormActivationState;
            result.IntroducedVersion = topForm.IntroducedVersion ?? result.IntroducedVersion;
            result.EntityLogicalName = topForm.EntityLogicalName ?? result.EntityLogicalName;
            // Labels: take from top layer if non-empty
            if (topForm.DisplayName.LocalizedLabels.Count > 0)
                result.DisplayName = topForm.DisplayName;
            if (topForm.Description.LocalizedLabels.Count > 0)
                result.Description = topForm.Description;
        }

        return result;
    }
}
