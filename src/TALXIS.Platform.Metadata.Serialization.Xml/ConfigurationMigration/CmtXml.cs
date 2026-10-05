namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>Lexical helpers shared by the CMT reader and writer.</summary>
internal static class CmtXml
{
    /// <summary>XmlSerializer boolean forms both importers accept: <c>true|false|1|0</c>. Anything else reads as false.</summary>
    public static bool ParseBool(string? value) => value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
