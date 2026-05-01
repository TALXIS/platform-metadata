namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class LookupAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Lookup;
    public string[] Targets { get; set; } = Array.Empty<string>();
    public CascadeType CascadeDelete { get; set; } = CascadeType.RemoveLink;
}
