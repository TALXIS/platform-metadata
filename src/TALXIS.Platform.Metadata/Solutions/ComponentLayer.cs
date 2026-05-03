namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Represents a single solution layer for a component.
/// Each managed solution import creates a layer; the active (unmanaged) layer sits on top.
/// </summary>
public sealed class ComponentLayer
{
    public required string SolutionName { get; set; }
    public required int Order { get; set; }
    public bool IsManaged { get; set; }
    public ComponentState State { get; set; } = ComponentState.Published;

    /// <summary>
    /// The component metadata for this layer. Can be any MetadataBase subclass
    /// (EntityMetadata, FormMetadata, SecurityRoleMetadata, etc.).
    /// For top-wins resolution, the active layer's Component is the effective value.
    /// For mergeable types, all layers' Components are inputs to the merge engine.
    /// </summary>
    public MetadataBase? Component { get; set; }
}
