namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class BigIntAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.BigInt;
    public long MinValue { get; set; } = -9223372036854775808;
    public long MaxValue { get; set; } = 9223372036854775807;
}
