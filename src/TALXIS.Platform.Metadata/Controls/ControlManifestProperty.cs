namespace TALXIS.Platform.Metadata.Controls;

/// <summary>
/// A single <c>property</c> element from a control manifest.
/// </summary>
public sealed class ControlManifestProperty
{
    public required string Name { get; init; }

    /// <summary>
    /// Dataverse type from <c>of-type</c> (e.g. <c>Enum</c>, <c>SingleLine.Text</c>, <c>Whole.None</c>, <c>Multiple</c>).
    /// </summary>
    public required string OfType { get; init; }

    public string? DefaultValue { get; init; }

    public bool Required { get; init; }

    /// <summary>
    /// Allowed values for <c>Enum</c> properties (the element texts of the <c>value</c> children).
    /// </summary>
    public IReadOnlyList<string> EnumValues { get; init; } = [];
}
