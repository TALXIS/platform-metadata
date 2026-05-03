namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Stable Dataverse component identity, independent of solution membership or source path.
/// </summary>
/// <param name="Type">Dataverse component type.</param>
/// <param name="ObjectId">Component object identifier within the type.</param>
public sealed record ComponentIdentity(ComponentType Type, string ObjectId);
