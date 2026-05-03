namespace TALXIS.Platform.Metadata.Components;

public sealed class SiteMapMetadata : MetadataBase
{
    public required string UniqueName { get; set; }
    public Label DisplayName { get; set; } = new();
    public string? IntroducedVersion { get; set; }
    public bool EnableCollapsibleGroups { get; set; }
    public bool ShowHome { get; set; }
    public bool ShowPinned { get; set; }
    public bool ShowRecents { get; set; }
}
