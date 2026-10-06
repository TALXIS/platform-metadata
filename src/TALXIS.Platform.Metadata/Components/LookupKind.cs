namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Which kind of lookup column a <see cref="Attributes.LookupAttributeMetadata"/> is. Dataverse stores all three as lookups but CMT and validation treat them differently.
/// </summary>
public enum LookupKind
{
    Lookup,
    Customer,
    Owner
}
