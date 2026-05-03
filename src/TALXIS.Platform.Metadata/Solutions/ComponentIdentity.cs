namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Stable Dataverse component identity, independent of solution membership or source path.
/// </summary>
public sealed record ComponentIdentity
{
    /// <summary>
    /// Creates a stable component identity.
    /// </summary>
    /// <param name="type">Dataverse component type.</param>
    /// <param name="objectId">Component object identifier within the type.</param>
    public ComponentIdentity(ComponentType type, string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Component object identifier is required.", nameof(objectId));

        Type = type;
        ObjectId = objectId;
    }

    /// <summary>
    /// Gets the Dataverse component type.
    /// </summary>
    public ComponentType Type { get; }

    /// <summary>
    /// Gets the component object identifier within the type.
    /// </summary>
    public string ObjectId { get; }
}
