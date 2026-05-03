namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Represents a component type that doesn't have a dedicated loader.
/// Preserves raw XML for roundtrip fidelity.
/// </summary>
public sealed class GenericComponentMetadata : MetadataBase
{
    /// <summary>
    /// The component type name derived from the root element or directory, e.g. "ConnectionRole", "EnvironmentVariableDefinition".
    /// </summary>
    public required string ComponentTypeName { get; set; }

    /// <summary>
    /// GUID identifier if one was found in the XML.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Display name if one was found in the XML.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Relative path within the workspace (e.g. "Other/ConnectionRoles.xml").
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Complete serialized content for roundtrip preservation.
    /// </summary>
    public string? SerializedContent { get; set; }
}
