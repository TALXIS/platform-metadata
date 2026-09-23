using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Components;

public sealed class WebResourceMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata, ICustomizableMetadata, IDeletableMetadata, ISolutionComponent
{
    public required string WebResourceId { get; set; }
    public required string Name { get; set; }

    /// <inheritdoc />
    public ComponentIdentity Identity => new(ComponentType.WebResource, WebResourceId);

    /// <inheritdoc />
    public string DocumentKey => $"WebResource:{Name}";
    public Label DisplayName { get; set; } = new();
    public int WebResourceType { get; set; }
    public string? FileName { get; set; }
    public string? IntroducedVersion { get; set; }
    public bool IsCustomizable { get; set; }
    public bool CanBeDeleted { get; set; }
    public bool IsHidden { get; set; }
    public bool IsEnabledForMobileClient { get; set; }
    public bool IsAvailableForMobileOffline { get; set; }
}
