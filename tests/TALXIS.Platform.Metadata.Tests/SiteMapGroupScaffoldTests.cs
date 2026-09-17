using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SiteMapGroupScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-sitemap-group-scaffold").FullName;
    private readonly string _siteMapPath;
    private readonly string _groupPath;

    public SiteMapGroupScaffoldTests()
    {
        var dir = Path.Combine(_root, "AppModuleSiteMaps", "udpp_warehouseapp");
        Directory.CreateDirectory(dir);
        _siteMapPath = Path.Combine(dir, "AppModuleSiteMap.xml");
        File.WriteAllText(_siteMapPath, """
            <AppModuleSiteMap>
              <SiteMap>
                <Area Id="area_1" ResourceId="SitemapDesigner.Warehouse" />
              </SiteMap>
            </AppModuleSiteMap>
            """);

        _groupPath = Path.Combine(_root, "group.xml");
        File.WriteAllText(_groupPath, """
            <Group Id="group_groupidexample" ResourceId="SitemapDesigner.Operations">
              <Titles><Title LCID="1033" Title="Operations" /></Titles>
            </Group>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SiteMapGroupScaffoldRequest Request(string areaTitle) => new()
    {
        SolutionRootPath = _root,
        SiteMapFilePath = _siteMapPath,
        GroupFilePath = _groupPath,
        AreaTitle = areaTitle,
    };

    [Fact]
    public void Apply_AppendsGroupIntoTargetArea()
    {
        SiteMapGroupScaffold.Apply(Request("Warehouse"));

        var group = XDocument.Load(_siteMapPath).Descendants("Area").Single().Element("Group");
        Assert.NotNull(group);
        Assert.StartsWith("group_", group!.Attribute("Id")!.Value);
        Assert.DoesNotContain("groupidexample", group.Attribute("Id")!.Value);
    }

    [Fact]
    public void Apply_UnknownArea_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SiteMapGroupScaffold.Apply(Request("Nope")));
    }
}
