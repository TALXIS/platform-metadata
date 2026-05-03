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

    /// <summary>Raw XML content of this layer (for merge operations).</summary>
    public string? XmlContent { get; set; }
}
