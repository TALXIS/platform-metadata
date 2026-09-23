using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Components;

public sealed class SiteMapMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata, ISolutionComponent
{
    public required string UniqueName { get; set; }

    /// <inheritdoc />
    public ComponentIdentity Identity => new(ComponentType.SiteMap, UniqueName);

    /// <inheritdoc />
    public string DocumentKey => $"SiteMap:{UniqueName}";
    public Label DisplayName { get; set; } = new();
    public string? IntroducedVersion { get; set; }
    public bool EnableCollapsibleGroups { get; set; }
    public bool ShowHome { get; set; }
    public bool ShowPinned { get; set; }
    public bool ShowRecents { get; set; }
    public MergeableNode? Body { get; set; }
}
