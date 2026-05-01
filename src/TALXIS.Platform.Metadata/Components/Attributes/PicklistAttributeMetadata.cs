namespace TALXIS.Platform.Metadata.Components.Attributes;

public class PicklistAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Picklist;
    public OptionSetMetadata? OptionSet { get; set; }
    public bool IsGlobal { get; set; }
}
