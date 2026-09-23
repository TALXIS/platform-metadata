using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Maps the OwnershipTypeMask tokens found in solution XML to <see cref="OwnershipType"/>; Dataverse writes both OrgOwned and OrganizationOwned.
/// </summary>
internal static class OwnershipTypeXml
{
    public static OwnershipType Parse(string? value) => value switch
    {
        "OrganizationOwned" or "OrgOwned" => OwnershipType.OrganizationOwned,
        "None" => OwnershipType.None,
        _ => OwnershipType.UserOwned
    };
}
