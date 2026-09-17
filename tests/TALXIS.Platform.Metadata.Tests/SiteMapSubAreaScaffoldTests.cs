using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SiteMapSubAreaScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-sitemap-subarea-scaffold").FullName;
    private readonly string _siteMapPath;

    public SiteMapSubAreaScaffoldTests()
    {
        var dir = Path.Combine(_root, "AppModuleSiteMaps", "udpp_warehouseapp");
        Directory.CreateDirectory(dir);
        _siteMapPath = Path.Combine(dir, "AppModuleSiteMap.xml");
        File.WriteAllText(_siteMapPath, """
            <AppModuleSiteMap>
              <SiteMap>
                <Area Id="area_1" ResourceId="SitemapDesigner.Warehouse">
                  <Group Id="group_1" ResourceId="SitemapDesigner.Operations" />
                </Area>
              </SiteMap>
            </AppModuleSiteMap>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SiteMapSubAreaScaffoldRequest Request() => new()
    {
        SolutionRootPath = _root,
        SiteMapFilePath = _siteMapPath,
        AreaTitle = "Warehouse",
        GroupTitle = "Operations",
        Title = "Items & \"Stock\"",
        PageType = "entity",
        EntityLogicalName = "udpp_warehouseitem",
    };

    private XElement AddedSubArea() =>
        XDocument.Load(_siteMapPath).Descendants("Group").Single().Element("SubArea")!;

    [Fact]
    public void Apply_EntityPage_BuildsSubAreaWithEscapedTitle()
    {
        SiteMapSubAreaScaffold.Apply(Request());

        var subArea = AddedSubArea();
        Assert.StartsWith("subarea_", subArea.Attribute("Id")!.Value);
        Assert.Equal("udpp_warehouseitem", subArea.Attribute("Entity")?.Value);
        Assert.Equal("Items & \"Stock\"", subArea.Descendants("Title").Single().Attribute("Title")?.Value);
        Assert.Equal("true", subArea.Attribute("AvailableOffline")?.Value);
    }

    [Fact]
    public void Apply_EntityListPage_BuildsUrl()
    {
        var request = Request();
        request.PageType = "entitylist";
        request.ViewId = "{f0000001-0000-4000-8000-000000000001}";

        SiteMapSubAreaScaffold.Apply(request);

        var url = AddedSubArea().Attribute("Url")?.Value;
        Assert.Equal("/main.aspx?pagetype=entitylist&etn=udpp_warehouseitem&viewid={f0000001-0000-4000-8000-000000000001}", url);
    }

    [Fact]
    public void Apply_GenPage_UsesVectorIcon()
    {
        var request = Request();
        request.PageType = "genpage";
        request.GenPageId = "gen123";

        SiteMapSubAreaScaffold.Apply(request);

        var subArea = AddedSubArea();
        Assert.Equal("gen123", subArea.Attribute("GenPageId")?.Value);
        Assert.Null(subArea.Attribute("Icon"));
        Assert.NotNull(subArea.Attribute("VectorIcon"));
    }

    [Fact]
    public void Apply_MissingRequiredParameter_Throws()
    {
        var request = Request();
        request.PageType = "dashboard";
        request.DashboardId = null;

        var ex = Assert.Throws<ArgumentException>(() => SiteMapSubAreaScaffold.Apply(request));
        Assert.Contains("DashboardId", ex.Message);
    }

    [Fact]
    public void Apply_UnknownPageType_Throws()
    {
        var request = Request();
        request.PageType = "banana";

        Assert.Throws<ArgumentException>(() => SiteMapSubAreaScaffold.Apply(request));
    }
}
