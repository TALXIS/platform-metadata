using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-sitemap-group template post-action scripts:
/// stamps a generated short id into the rendered group payload and appends it to
/// the target area of the app's sitemap.
/// </summary>
public static class SiteMapGroupScaffold
{
    public static ScaffoldResult Apply(SiteMapGroupScaffoldRequest request)
    {
        var siteMapPath = ManagedXmlFileLocator.Locate(request.SiteMapFilePath)
            ?? throw new FileNotFoundException($"Could not find AppModuleSiteMap XML file at '{request.SiteMapFilePath}'.");

        var groupDoc = new XmlDocument();
        groupDoc.Load(request.GroupFilePath);
        groupDoc.LoadXml(groupDoc.OuterXml.Replace("groupidexample", SiteMapIdGenerator.NewShortId()));

        var siteMapDoc = ScaffoldXmlFile.Load(siteMapPath);
        var areaNode = siteMapDoc.SelectSingleNode($"//SiteMap/Area[@ResourceId='SitemapDesigner.{request.AreaTitle}']")
            ?? throw new InvalidOperationException($"Could not find the Area node for '{request.AreaTitle}' in '{siteMapPath}'.");
        areaNode.AppendChild(siteMapDoc.ImportNode(groupDoc.DocumentElement!, true));

        ScaffoldXmlFile.Save(siteMapDoc, siteMapPath);
        return new ScaffoldResult();
    }
}
