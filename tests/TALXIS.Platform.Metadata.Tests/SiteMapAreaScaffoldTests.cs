using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SiteMapAreaScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-sitemap-area-scaffold").FullName;
    private readonly string _siteMapPath;
    private readonly string _areaPath;

    public SiteMapAreaScaffoldTests()
    {
        var dir = Path.Combine(_root, "AppModuleSiteMaps", "udpp_warehouseapp");
        Directory.CreateDirectory(dir);
        _siteMapPath = Path.Combine(dir, "AppModuleSiteMap.xml");
        File.WriteAllText(_siteMapPath, """
            <AppModuleSiteMap>
              <SiteMap>
                <Area Id="area_existing" ResourceId="SitemapDesigner.Existing" />
              </SiteMap>
            </AppModuleSiteMap>
            """);

        _areaPath = Path.Combine(_root, "area.xml");
        File.WriteAllText(_areaPath, """
            <Area Id="area_areaidexample" ResourceId="SitemapDesigner.Warehouse" ShowGroups="true">
              <Titles><Title LCID="1033" Title="Warehouse" /></Titles>
            </Area>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_AppendsAreaWithGeneratedId()
    {
        SiteMapAreaScaffold.Apply(new SiteMapAreaScaffoldRequest
        {
            SolutionRootPath = _root,
            SiteMapFilePath = _siteMapPath,
            AreaFilePath = _areaPath,
        });

        var areas = XDocument.Load(_siteMapPath).Descendants("Area").ToList();
        Assert.Equal(2, areas.Count);
        var added = areas.Single(a => a.Attribute("ResourceId")?.Value == "SitemapDesigner.Warehouse");
        var id = added.Attribute("Id")!.Value;
        Assert.StartsWith("area_", id);
        Assert.DoesNotContain("areaidexample", id);
        Assert.Equal(8, id.Length - "area_".Length);
    }

    [Fact]
    public void Apply_MissingSiteMap_Throws()
    {
        File.Delete(_siteMapPath);

        Assert.Throws<FileNotFoundException>(() => SiteMapAreaScaffold.Apply(new SiteMapAreaScaffoldRequest
        {
            SolutionRootPath = _root,
            SiteMapFilePath = _siteMapPath,
            AreaFilePath = _areaPath,
        }));
    }
}
