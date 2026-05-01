namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class IntegerAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Integer;
    public int MinValue { get; set; } = -2147483648;
    public int MaxValue { get; set; } = 2147483647;
    public IntegerFormat Format { get; set; } = IntegerFormat.None;
}

public enum IntegerFormat { None, Duration, TimeZone, Language, Locale }
