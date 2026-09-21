using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class AppModelComponentScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-app-model-component-scaffold").FullName;
    private readonly string _appModulePath;

    public AppModelComponentScaffoldTests()
    {
        var dir = Path.Combine(_root, "AppModules", "udpp_warehouseapp");
        Directory.CreateDirectory(dir);
        _appModulePath = Path.Combine(dir, "AppModule.xml");
        File.WriteAllText(_appModulePath, """
            <AppModule><AppModuleComponents></AppModuleComponents></AppModule>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_EntityType_RegistersBySchemaName()
    {
        AppModelComponentScaffold.Apply(new AppModelComponentScaffoldRequest
        {
            SolutionRootPath = _root,
            AppModuleFilePath = _appModulePath,
            ComponentTypeId = "1",
            EntitySchemaName = "udpp_warehouseitem",
        });

        var component = XDocument.Load(_appModulePath).Descendants("AppModuleComponent").Single();
        Assert.Equal("1", component.Attribute("type")?.Value);
        Assert.Equal("udpp_warehouseitem", component.Attribute("schemaName")?.Value);
        Assert.Null(component.Attribute("id"));
    }

    [Fact]
    public void Apply_NonEntityType_RegistersByBracedId()
    {
        AppModelComponentScaffold.Apply(new AppModelComponentScaffoldRequest
        {
            SolutionRootPath = _root,
            AppModuleFilePath = _appModulePath,
            ComponentTypeId = "60",
            ComponentId = "c1b2c3d4-e5f6-4a1b-8c2d-000000000080",
        });

        var component = XDocument.Load(_appModulePath).Descendants("AppModuleComponent").Single();
        Assert.Equal("60", component.Attribute("type")?.Value);
        Assert.Equal("{c1b2c3d4-e5f6-4a1b-8c2d-000000000080}", component.Attribute("id")?.Value);
        Assert.Null(component.Attribute("schemaName"));
    }

    [Fact]
    public void Apply_MissingAppModule_WarnsAndSkips()
    {
        File.Delete(_appModulePath);

        var result = AppModelComponentScaffold.Apply(new AppModelComponentScaffoldRequest
        {
            SolutionRootPath = _root,
            AppModuleFilePath = _appModulePath,
            ComponentTypeId = "1",
            EntitySchemaName = "udpp_warehouseitem",
        });

        Assert.Single(result.Warnings);
    }
}
