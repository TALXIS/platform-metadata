namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class ImageAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Image;
    public int MaxSizeInKB { get; set; } = 10240;
    public bool CanStoreFullImage { get; set; }
}
