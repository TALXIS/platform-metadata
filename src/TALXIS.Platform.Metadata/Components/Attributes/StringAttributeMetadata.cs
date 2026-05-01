namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class StringAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.String;
    public int MaxLength { get; set; } = 100;
    public StringFormatName FormatName { get; set; } = StringFormatName.Text;
}

public enum StringFormatName { Text, Email, Url, Phone, TextArea, TickerSymbol }
