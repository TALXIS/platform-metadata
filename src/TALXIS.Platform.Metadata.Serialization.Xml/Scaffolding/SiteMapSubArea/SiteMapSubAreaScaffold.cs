using System.Security;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Builds a SubArea element for one of seven page types (entity, entitylist,
/// dashboard, control, custom, webresource or genpage), assigns a generated short id,
/// and appends it to the target group of the app's sitemap.
/// </summary>
public static class SiteMapSubAreaScaffold
{
    private const string CommonAttributes =
        "Client=\"All,Outlook,OutlookLaptopClient,OutlookWorkstationClient,Web\" AvailableOffline=\"true\" PassParams=\"false\" Sku=\"All,OnPremise,Live,SPLA\"";

    public static ScaffoldResult Apply(SiteMapSubAreaScaffoldRequest request)
    {
        var siteMapPath = ManagedXmlFileLocator.Locate(request.SiteMapFilePath)
            ?? throw new FileNotFoundException($"Could not find AppModuleSiteMap XML file at '{request.SiteMapFilePath}'.");

        var siteMapDoc = ScaffoldXmlFile.Load(siteMapPath);
        var groupNode = siteMapDoc.SelectSingleNode(
                $"//SiteMap/Area[@ResourceId='SitemapDesigner.{request.AreaTitle}']/Group[@ResourceId='SitemapDesigner.{request.GroupTitle}']")
            ?? throw new InvalidOperationException($"Could not find the Group node for '{request.GroupTitle}' in '{siteMapPath}'.");

        var subAreaDoc = new XmlDocument();
        subAreaDoc.LoadXml(BuildSubAreaXml(request));
        groupNode.AppendChild(siteMapDoc.ImportNode(subAreaDoc.DocumentElement!, true));

        ScaffoldXmlFile.Save(siteMapDoc, siteMapPath);
        return new ScaffoldResult();
    }

    private static string BuildSubAreaXml(SiteMapSubAreaScaffoldRequest request)
    {
        var id = "subarea_" + SiteMapIdGenerator.NewShortId();
        var titles = $"<Titles><Title LCID=\"1033\" Title=\"{SecurityElement.Escape(request.Title)}\" /></Titles>";

        switch (request.PageType)
        {
            case "entity":
                Require(request.EntityLogicalName, "EntityLogicalName", "entity");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Entity=\"{request.EntityLogicalName}\" {CommonAttributes}>{titles}</SubArea>";
            case "entitylist":
                Require(request.EntityLogicalName, "EntityLogicalName", "entitylist");
                Require(request.ViewId, "ViewId", "entitylist");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Url=\"/main.aspx?pagetype=entitylist&amp;etn={request.EntityLogicalName}&amp;viewid={request.ViewId}\" {CommonAttributes}>{titles}</SubArea>";
            case "dashboard":
                Require(request.DashboardId, "DashboardId", "dashboard");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Url=\"/workplace/home_dashboards.aspx\" DefaultDashboard=\"{request.DashboardId}\" {CommonAttributes}>{titles}</SubArea>";
            case "control":
                Require(request.ControlName, "ControlName", "control");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Url=\"/main.aspx?pagetype=control&amp;controlName={request.ControlName}\" Entity=\"{request.EntityLogicalName}\" {CommonAttributes}>{titles}</SubArea>";
            case "custom":
                Require(request.CustomPageName, "CustomPageName", "custom");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Url=\"/main.aspx?pagetype=custom&amp;name={request.CustomPageName}\" {CommonAttributes}>{titles}</SubArea>";
            case "webresource":
                Require(request.WebResourceName, "WebResourceName", "webresource");
                return $"<SubArea Id=\"{id}\" Icon=\"/_imgs/imagestrips/transparent_spacer.gif\" Url=\"/main.aspx?pagetype=webresource&amp;webresourceName={request.WebResourceName}\" {CommonAttributes}>{titles}</SubArea>";
            case "genpage":
                Require(request.GenPageId, "GenPageId", "genpage");
                return $"<SubArea Id=\"{id}\" GenPageId=\"{request.GenPageId}\" VectorIcon=\"/_imgs/TableIconsFluentV9/document_one_page_sparkle.svg\" {CommonAttributes}>{titles}</SubArea>";
            default:
                throw new ArgumentException($"Unknown PageType: {request.PageType}");
        }
    }

    private static void Require(string? value, string name, string pageType)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required when PageType is '{pageType}'");
    }
}
