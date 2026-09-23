namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Boolean tokens as they appear in solution XML: 1/0 from SolutionPackager, true/false from hand-edited files.
/// </summary>
internal static class XmlBooleans
{
    public static bool IsTrue(string? value) =>
        value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
