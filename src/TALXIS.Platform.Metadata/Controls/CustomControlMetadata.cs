namespace TALXIS.Platform.Metadata.Controls;

/// <summary>
/// A custom (PCF) control owned by a solution, pairing its publisher-prefixed name
/// with the parsed control manifest. Its registry identity is
/// <see cref="ComponentType.CustomControl"/> (name-based, unpacked to <c>Controls/</c>).
/// </summary>
public sealed class CustomControlMetadata : MetadataBase
{
    /// <summary>
    /// Publisher-prefixed name used in FormXml (e.g. <c>talxis_TALXIS.PCF.Grid</c>).
    /// Null when the control was read from a source project, where no solution context exists.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Parsed <c>ControlManifest.xml</c> of the control.
    /// </summary>
    public required ControlManifestInfo Manifest { get; init; }
}
