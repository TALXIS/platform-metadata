namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Source-owned component payload loaded from a solution project.
/// Multiple unmanaged solution projects can have snapshots for the same component while Dataverse still exposes one Active layer.
/// </summary>
public sealed class ComponentSourceSnapshot
{
    /// <summary>
    /// Gets or sets the solution project that owns this source payload.
    /// </summary>
    public required string SourceSolutionUniqueName { get; set; }

    /// <summary>
    /// Gets or sets the component identity.
    /// </summary>
    public required ComponentIdentity Identity { get; set; }

    /// <summary>
    /// Gets or sets the loaded metadata payload, when a typed payload exists.
    /// </summary>
    public MetadataBase? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the source project root.
    /// </summary>
    public string? SourceRootPath { get; set; }

    /// <summary>
    /// Gets or sets the source document key/path within the project.
    /// </summary>
    public string? SourceDocumentKey { get; set; }

    /// <summary>
    /// Gets or sets caller-defined source precedence order.
    /// </summary>
    public int SourceOrder { get; set; }

    /// <summary>
    /// Gets or sets whether the source project is managed.
    /// </summary>
    public bool IsManaged { get; set; }
}
