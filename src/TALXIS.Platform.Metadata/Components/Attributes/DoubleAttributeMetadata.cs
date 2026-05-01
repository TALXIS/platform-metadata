namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class DoubleAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Double;
    public double MinValue { get; set; } = -100000000000;
    public double MaxValue { get; set; } = 100000000000;
    public int Precision { get; set; } = 2;
}
