namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Describes one metadata payload that should be imported as part of a solution layer or source snapshot.
/// </summary>
public sealed record LayerComponentDescriptor
{
    /// <summary>
    /// Creates a component descriptor.
    /// </summary>
    /// <param name="type">Dataverse component type.</param>
    /// <param name="objectId">Stable component identifier within the type.</param>
    /// <param name="metadata">Typed metadata payload for the layer, if one is available.</param>
    /// <param name="sourceDocumentKey">Optional source document key/path for diagnostics and write-back.</param>
    public LayerComponentDescriptor(ComponentType type, string objectId, MetadataBase? metadata, string? sourceDocumentKey = null)
        : this(new ComponentIdentity(type, objectId), metadata, sourceDocumentKey)
    {
    }

    /// <summary>
    /// Creates a component descriptor.
    /// </summary>
    /// <param name="identity">Stable component identity.</param>
    /// <param name="metadata">Typed metadata payload for the layer, if one is available.</param>
    /// <param name="sourceDocumentKey">Optional source document key/path for diagnostics and write-back.</param>
    public LayerComponentDescriptor(ComponentIdentity identity, MetadataBase? metadata, string? sourceDocumentKey = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Metadata = metadata;
        SourceDocumentKey = sourceDocumentKey;
    }

    /// <summary>
    /// Gets the stable component identity.
    /// </summary>
    public ComponentIdentity Identity { get; }

    /// <summary>
    /// Gets the Dataverse component type.
    /// </summary>
    public ComponentType Type => Identity.Type;

    /// <summary>
    /// Gets the stable component identifier within the type.
    /// </summary>
    public string ObjectId => Identity.ObjectId;

    /// <summary>
    /// Gets the typed metadata payload for the layer, if one is available.
    /// </summary>
    public MetadataBase? Metadata { get; }

    /// <summary>
    /// Gets the optional source document key/path for diagnostics and write-back.
    /// </summary>
    public string? SourceDocumentKey { get; }
}
