using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Helpers for the EntityRelationships XML files (Other/Relationships.xml and
/// the per-entity Other/Relationships/*.xml), shared by the entity-attribute
/// lookup step and the bpf relationship step.
/// </summary>
internal static class RelationshipsXmlFile
{
    private const string EmptyRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><EntityRelationships xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"></EntityRelationships>";

    public static void EnsureExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        if (File.Exists(path)) return;

        var doc = new XmlDocument();
        doc.LoadXml(EmptyRelationshipsXml);
        doc.Save(path);
    }

    public static bool ContainsRelationship(XmlDocument doc, string relationshipName)
    {
        foreach (XmlElement node in doc.GetElementsByTagName("EntityRelationship"))
        {
            if (node.GetAttribute("Name") == relationshipName) return true;
        }
        return false;
    }

    public static void AppendNameStub(XmlDocument doc, string relationshipName)
    {
        var stub = doc.CreateElement("EntityRelationship");
        stub.SetAttribute("Name", relationshipName);
        doc.DocumentElement!.AppendChild(stub);
    }
}
