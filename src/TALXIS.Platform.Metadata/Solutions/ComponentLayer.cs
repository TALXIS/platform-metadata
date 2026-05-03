namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Represents a single solution layer for a component.
/// Each managed solution import creates a layer; the active (unmanaged) layer sits on top.
/// </summary>
public sealed class ComponentLayer
{
    /// <summary>
    /// Gets or sets the Dataverse layer solution name, such as a managed solution name or <c>Active</c>.
    /// </summary>
    public required string SolutionUniqueName { get; set; }

    /// <summary>
    /// Gets or sets the solution project that owns the source payload.
    /// </summary>
    public string? SourceSolutionUniqueName { get; set; }

    /// <summary>
    /// Gets or sets the Dataverse-style layer kind.
    /// </summary>
    public SolutionLayerKind LayerKind { get; set; } = SolutionLayerKind.Managed;

    /// <summary>
    /// Gets or sets the optional source project root.
    /// </summary>
    public string? SourceRootPath { get; set; }

    /// <summary>
    /// Gets or sets the optional source document key/path.
    /// </summary>
    public string? SourceDocumentKey { get; set; }

    /// <summary>
    /// Gets or sets the caller-defined source project order.
    /// </summary>
    public int SourceOrder { get; set; }

    public required int Order { get; set; }
    public bool IsManaged { get; set; }
    public ComponentState State { get; set; } = ComponentState.Publish;

    /// <summary>
    /// Gets or sets the optional solution row identifier when available from Dataverse.
    /// </summary>
    public Guid? SolutionId { get; set; }

    /// <summary>
    /// Gets or sets the optional supporting solution identifier when available from Dataverse.
    /// </summary>
    public Guid? SupportingSolutionId { get; set; }

    /// <summary>
    /// Gets or sets the optional Dataverse overwrite time used to order live layers.
    /// </summary>
    public DateTimeOffset? OverwriteTime { get; set; }

    /// <summary>
    /// The component metadata for this layer. Can be any MetadataBase subclass
    /// (EntityMetadata, FormMetadata, SecurityRoleMetadata, etc.).
    /// For top-wins resolution, the active layer's Component is the effective value.
    /// For mergeable types, all layers' Components are inputs to the merge engine.
    /// </summary>
    public MetadataBase? Component { get; set; }
}
