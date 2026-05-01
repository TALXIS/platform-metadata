namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class BooleanAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Boolean;
    public BooleanOptionSetMetadata OptionSet { get; set; } = new();
}
