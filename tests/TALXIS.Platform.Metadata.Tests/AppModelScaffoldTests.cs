using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class AppModelScaffoldTests : IDisposable
{
    private const string AppName = "udpp_warehouseapp";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-app-model-scaffold").FullName;
    private readonly string _siteMapPath;

    public AppModelScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);

        var dir = Path.Combine(_root, "AppModuleSiteMaps", AppName);
        Directory.CreateDirectory(dir);
        _siteMapPath = Path.Combine(dir, "AppModuleSiteMap.xml");
        File.WriteAllText(_siteMapPath, """
            <AppModuleSiteMap><SiteMap><Area Id="area_areaidexample"><Group Id="group_groupidexample"><SubArea Id="subarea_subareaidexample" /></Group></Area></SiteMap></AppModuleSiteMap>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ScaffoldResult Apply() => AppModelScaffold.Apply(new AppModelScaffoldRequest
    {
        SolutionRootPath = _root,
        AppSchemaName = AppName,
        SiteMapFilePath = _siteMapPath,
    });

    [Fact]
    public void Apply_StampsIdsAndRegistersBothRootComponents()
    {
        Apply();

        var text = File.ReadAllText(_siteMapPath);
        Assert.DoesNotContain("areaidexample", text);
        Assert.DoesNotContain("groupidexample", text);
        Assert.DoesNotContain("subareaidexample", text);

        var components = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"))
            .Descendants("RootComponent")
            .Select(rc => (rc.Attribute("type")?.Value, rc.Attribute("schemaName")?.Value))
            .ToList();
        Assert.Equal(new[] { ("80", AppName), ("62", AppName) }, components);
    }

    [Fact]
    public void Apply_ManagedSiteMapVariant_IsFound()
    {
        var managed = Path.Combine(Path.GetDirectoryName(_siteMapPath)!, "AppModuleSiteMap_managed.xml");
        File.Move(_siteMapPath, managed);

        var result = Apply();

        Assert.Empty(result.Warnings);
        Assert.DoesNotContain("areaidexample", File.ReadAllText(managed));
    }

    [Fact]
    public void Apply_MissingSiteMap_WarnsButStillRegisters()
    {
        File.Delete(_siteMapPath);

        var result = Apply();

        Assert.Single(result.Warnings);
        Assert.Equal(2, XDocument.Load(Path.Combine(_root, "Other", "Solution.xml")).Descendants("RootComponent").Count());
    }
}
