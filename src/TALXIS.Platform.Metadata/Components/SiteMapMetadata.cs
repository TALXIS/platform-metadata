using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Components;

public sealed class SiteMapMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata
{
    public required string UniqueName { get; set; }
    public Label DisplayName { get; set; } = new();
    public string? IntroducedVersion { get; set; }
    public bool EnableCollapsibleGroups { get; set; }
    public bool ShowHome { get; set; }
    public bool ShowPinned { get; set; }
    public bool ShowRecents { get; set; }
    public MergeableNode? Body { get; set; }
}
