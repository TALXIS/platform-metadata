using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Components;

public sealed class RibbonMetadata : MetadataBase
{
    public string? EntityLogicalName { get; set; }
    public MergeableNode? Body { get; set; }
}
