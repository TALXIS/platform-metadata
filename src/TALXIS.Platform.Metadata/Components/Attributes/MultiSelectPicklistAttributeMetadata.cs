namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class MultiSelectPicklistAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.MultiSelectPicklist;
    public OptionSetMetadata? OptionSet { get; set; }
    public bool IsGlobal { get; set; }
}
