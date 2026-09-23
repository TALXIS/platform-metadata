using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// A loaded component seen through its workspace: the typed metadata plus the layers, memberships and source snapshots that surround it.
/// </summary>
public sealed class WorkspaceComponent
{
    private readonly Workspace _workspace;

    internal WorkspaceComponent(Workspace workspace, ISolutionComponent component)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Component = component ?? throw new ArgumentNullException(nameof(component));
    }

    /// <summary>
    /// The component contract this view wraps.
    /// </summary>
    public ISolutionComponent Component { get; }

    /// <summary>
    /// Stable identity (type + object id).
    /// </summary>
    public ComponentIdentity Identity => Component.Identity;

    /// <summary>
    /// The typed metadata object to read or mutate.
    /// </summary>
    public MetadataBase Metadata => (MetadataBase)Component;

    /// <summary>
    /// Solution layers that contribute to this component, bottom to top; empty when no layer was registered.
    /// </summary>
    public IReadOnlyList<ComponentLayer> Layers =>
        _workspace.Layers.FindStack(Identity.Type, Identity.ObjectId)?.Layers ?? Array.Empty<ComponentLayer>();

    /// <summary>
    /// The effective state after layering: top-wins for most types, merge for forms, site maps, app modules and ribbons; null when the component is deleted in its top layer or has no layers.
    /// </summary>
    public MetadataBase? ActiveState
    {
        get
        {
            var stack = _workspace.Layers.FindStack(Identity.Type, Identity.ObjectId);
            return stack == null ? null : _workspace.Layers.Resolve(stack);
        }
    }

    /// <summary>
    /// Solution.xml root-component rows that reference this component.
    /// </summary>
    public IReadOnlyList<SolutionComponentMembership> Memberships => _workspace.GetMemberships(Identity);

    /// <summary>
    /// Source projects that carry a payload for this component, the write-back targets.
    /// </summary>
    public IReadOnlyList<ComponentSourceSnapshot> Snapshots => _workspace.GetSourceSnapshots(Identity);
}
