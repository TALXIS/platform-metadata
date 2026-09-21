namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Generates short ids for Area, Group and SubArea elements using
/// the first segment of a new GUID.
/// </summary>
internal static class SiteMapIdGenerator
{
    public static string NewShortId() => Guid.NewGuid().ToString().Split('-')[0];
}
