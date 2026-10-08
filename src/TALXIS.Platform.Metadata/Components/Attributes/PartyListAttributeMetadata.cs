namespace TALXIS.Platform.Metadata.Components.Attributes;

/// <summary>
/// An activity party list column (<c>partylist</c> in Entity.xml), such as the recipients of an email.
/// </summary>
public sealed class PartyListAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.PartyList;
}
