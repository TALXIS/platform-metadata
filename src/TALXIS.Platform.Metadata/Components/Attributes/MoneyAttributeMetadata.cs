namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class MoneyAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Money;
    public double MinValue { get; set; } = -922337203685477;
    public double MaxValue { get; set; } = 922337203685477;
    public int Precision { get; set; } = 2;
    public int PrecisionSource { get; set; }
}
