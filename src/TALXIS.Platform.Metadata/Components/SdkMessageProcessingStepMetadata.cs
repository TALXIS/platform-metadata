namespace TALXIS.Platform.Metadata.Components;

public sealed class SdkMessageProcessingStepMetadata : MetadataBase
{
    public required string SdkMessageProcessingStepId { get; set; }
    public string? Name { get; set; }
    public string? SdkMessageId { get; set; }
    public string? PluginTypeName { get; set; }
    public string? PluginTypeId { get; set; }
    public int? Stage { get; set; }
    public int? Mode { get; set; }
    public int? Rank { get; set; }
    public string? FilteringAttributes { get; set; }
    public bool AsyncAutoDelete { get; set; }
    public string? Description { get; set; }
    public int? SupportedDeployment { get; set; }
    public int? InvocationSource { get; set; }
    public int? EventHandlerTypeCode { get; set; }
    public string? IntroducedVersion { get; set; }
    public bool IsCustomizable { get; set; }
    public bool IsHidden { get; set; }

    private readonly List<SdkMessageProcessingStepImageMetadata> _images = new();
    public IReadOnlyList<SdkMessageProcessingStepImageMetadata> Images => _images;

    public void AddImage(SdkMessageProcessingStepImageMetadata image) => _images.Add(image);
}
