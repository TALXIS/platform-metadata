using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Stamps a generated short id into the rendered area payload and appends it
/// to the app's SiteMap element.
/// </summary>
public static class SiteMapAreaScaffold
{
    public static ScaffoldResult Apply(SiteMapAreaScaffoldRequest request)
    {
        var siteMapPath = ManagedXmlFileLocator.Locate(request.SiteMapFilePath)
            ?? throw new FileNotFoundException($"Could not find AppModuleSiteMap XML file at '{request.SiteMapFilePath}'.");

        var areaDoc = new XmlDocument();
        areaDoc.Load(request.AreaFilePath);
        areaDoc.LoadXml(areaDoc.OuterXml.Replace("areaidexample", SiteMapIdGenerator.NewShortId()));

        var siteMapDoc = ScaffoldXmlFile.Load(siteMapPath);
        var siteMapNode = siteMapDoc.SelectSingleNode("//SiteMap")
            ?? throw new InvalidOperationException($"Could not find SiteMap node in '{siteMapPath}'.");
        siteMapNode.AppendChild(siteMapDoc.ImportNode(areaDoc.DocumentElement!, true));

        ScaffoldXmlFile.Save(siteMapDoc, siteMapPath);
        return new ScaffoldResult();
    }
}
