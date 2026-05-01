namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class MemoAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Memo;
    public int MaxLength { get; set; } = 2000;
}
