using System.Text;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// XML load/save helpers shared by the scaffold appliers, using the same writer
/// settings as the original template scripts so output stays byte-compatible.
/// </summary>
internal static class ScaffoldXmlFile
{
    public static XmlDocument Load(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    public static void Save(XmlDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, CreateWriterSettings());
        doc.Save(writer);
    }

    public static void Save(System.Xml.Linq.XDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, CreateWriterSettings());
        doc.Save(writer);
    }

    private static XmlWriterSettings CreateWriterSettings() => new()
    {
        Indent = true,
        NewLineHandling = NewLineHandling.None,
        OmitXmlDeclaration = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
    };
}
