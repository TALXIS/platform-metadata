namespace TALXIS.Platform.Metadata.Components;

public sealed class WebResourceMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata, ICustomizableMetadata, IDeletableMetadata
{
    public required string WebResourceId { get; set; }
    public required string Name { get; set; }
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
