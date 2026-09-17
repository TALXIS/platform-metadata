namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Generates the short ids the sitemap templates stamp into Area/Group/SubArea
/// elements: the first segment of a fresh GUID.
/// </summary>
internal static class SiteMapIdGenerator
{
    public static string NewShortId() => Guid.NewGuid().ToString().Split('-')[0];
}
