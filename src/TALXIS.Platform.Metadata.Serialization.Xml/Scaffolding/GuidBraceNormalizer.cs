using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Fixup for component files whose GUID lost its braces during template rendering:
/// braces the id inside the file and renames the file to the braced form.
/// Shared by the pp-entity-view and pp-entity-form style SetFormId scripts.
/// </summary>
internal static class GuidBraceNormalizer
{
    /// <summary>
    /// Normalizes the first unbraced XML file in the directory; returns the braced
    /// file path, or null when every file already has braces.
    /// </summary>
    public static string? NormalizeFirstUnbraced(string directory, string idNodeXPath)
    {
        var unbracedFilePath = Directory.GetFiles(directory, "*.xml")
            .FirstOrDefault(f => !Path.GetFileNameWithoutExtension(f).StartsWith("{", StringComparison.Ordinal));
        if (unbracedFilePath == null) return null;

        var bracedId = "{" + Path.GetFileNameWithoutExtension(unbracedFilePath) + "}";

        // Plain XmlDocument.Save keeps output identical to the original PowerShell fixup.
        var doc = new XmlDocument();
        doc.Load(unbracedFilePath);
        var idNode = doc.SelectSingleNode(idNodeXPath);
        if (idNode != null) idNode.InnerText = bracedId;
        doc.Save(unbracedFilePath);

        var bracedFilePath = Path.Combine(directory, bracedId + ".xml");
        if (!string.Equals(unbracedFilePath, bracedFilePath, StringComparison.OrdinalIgnoreCase))
            File.Move(unbracedFilePath, bracedFilePath);
        return bracedFilePath;
    }
}
