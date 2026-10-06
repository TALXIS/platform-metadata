namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class LookupAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Lookup;
    public string[] Targets { get; set; } = Array.Empty<string>();
    public CascadeType CascadeDelete { get; set; } = CascadeType.RemoveLink;

    /// <summary>
    /// Plain lookup, customer (account or contact) or owner (user or team); <see cref="AttributeType"/> stays Lookup for all three.
    /// </summary>
    public LookupKind LookupKind { get; set; } = LookupKind.Lookup;
}
