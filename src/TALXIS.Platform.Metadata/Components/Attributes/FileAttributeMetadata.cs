namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class FileAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.File;
    public int MaxSizeInKB { get; set; } = 32768;
}
