using System.Xml;
using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Sorts attributes by PhysicalName in every Entity.xml, using the order
/// maintained by SolutionPackager.
/// </summary>
internal static class EntityAttributeSorter
{
    public static void SortAll(string solutionRootPath)
    {
        var entitiesDir = Path.Combine(solutionRootPath, SolutionPackagerLayout.EntitiesDirectory);
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityXmlPath in Directory.GetFiles(entitiesDir, "Entity.xml", SearchOption.AllDirectories))
        {
            var doc = ScaffoldXmlFile.Load(entityXmlPath);
            foreach (XmlNode attributesNode in doc.SelectNodes("//entity/attributes")!)
            {
                var attributes = attributesNode.SelectNodes("attribute")!.Cast<XmlElement>().ToList();
                if (attributes.Count == 0) continue;

                var sorted = attributes.OrderBy(a => a.GetAttribute("PhysicalName").ToLowerInvariant()).ToList();
                foreach (var attribute in attributes)
                {
                    attributesNode.RemoveChild(attribute);
                }
                foreach (var attribute in sorted)
                {
                    attributesNode.AppendChild(attribute);
                }
            }
            ScaffoldXmlFile.Save(doc, entityXmlPath);
        }
    }
}
