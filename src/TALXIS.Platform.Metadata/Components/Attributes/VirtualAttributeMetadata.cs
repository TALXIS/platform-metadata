namespace TALXIS.Platform.Metadata.Components.Attributes;

/// <summary>
/// A column Dataverse computes and does not store (<c>virtual</c> in Entity.xml). Image and multi-select columns, which Dataverse also
/// reports as virtual, keep their own types.
/// </summary>
public sealed class VirtualAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.Virtual;
}
