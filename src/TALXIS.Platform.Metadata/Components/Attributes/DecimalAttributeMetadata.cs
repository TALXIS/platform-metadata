namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class DecimalAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Decimal;
    public decimal MinValue { get; set; } = -100000000000m;
    public decimal MaxValue { get; set; } = 100000000000m;
    public int Precision { get; set; } = 2;
}
