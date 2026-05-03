namespace TALXIS.Platform.Metadata.Components;

public sealed class SdkMessageProcessingStepImageMetadata : MetadataBase
{
    public required string SdkMessageProcessingStepImageId { get; set; }
    public int? ImageType { get; set; }
    public string? MessagePropertyName { get; set; }
    public string? EntityAlias { get; set; }
    public string? Attributes { get; set; }
}
