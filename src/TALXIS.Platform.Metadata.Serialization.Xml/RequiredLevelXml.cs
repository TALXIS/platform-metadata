using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

internal static class RequiredLevelXml
{
    public static RequiredLevel Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "required" => RequiredLevel.Required,
            "applicationrequired" => RequiredLevel.ApplicationRequired,
            "systemrequired" => RequiredLevel.SystemRequired,
            "recommended" => RequiredLevel.Recommended,
            _ => RequiredLevel.None
        };
    }

    public static string ToXmlValue(RequiredLevel value)
    {
        return value switch
        {
            RequiredLevel.Required => "required",
            RequiredLevel.ApplicationRequired => "applicationrequired",
            RequiredLevel.SystemRequired => "systemrequired",
            RequiredLevel.Recommended => "recommended",
            _ => "none"
        };
    }
}
