using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Components;

public sealed class RibbonMetadata : MetadataBase, ISolutionComponent
{
    private const string GlobalRibbonObjectId = "global";

    public string? EntityLogicalName { get; set; }
    public MergeableNode? Body { get; set; }

    /// <inheritdoc />
    public ComponentIdentity Identity => new(ComponentType.RibbonCustomization, EntityLogicalName ?? GlobalRibbonObjectId);

    /// <inheritdoc />
    public string DocumentKey => $"Ribbon:{EntityLogicalName ?? GlobalRibbonObjectId}";
}
