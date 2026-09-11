using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionRootComponentPatcherTests : IDisposable
{
    private static readonly Guid RoleId = Guid.Parse("a1b2c3d4-e5f6-4a1b-8c2d-000000000001");

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-root-component-patcher").FullName;
    private readonly string _solutionXmlPath;

    public SolutionRootComponentPatcherTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        _solutionXmlPath = Path.Combine(_root, "Other", "Solution.xml");
        File.WriteAllText(_solutionXmlPath, """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IEnumerable<XElement> RootComponentNodes() =>
        XDocument.Load(_solutionXmlPath).Descendants("RootComponent");

    [Fact]
    public void EnsureRootComponent_AddsSchemaNameComponent()
    {
        var added = SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = "udpp_warehouseitem",
            Behavior = 0,
        });

        Assert.True(added);
        var node = RootComponentNodes().Single();
        Assert.Equal("1", node.Attribute("type")?.Value);
        Assert.Equal("udpp_warehouseitem", node.Attribute("schemaName")?.Value);
    }

    [Fact]
    public void EnsureRootComponent_ExistingComponent_IsNotDuplicated()
    {
        var component = new RootComponent { Type = ComponentType.Entity, SchemaName = "udpp_warehouseitem" };
        Assert.True(SolutionRootComponentPatcher.EnsureRootComponent(_root, component));
        Assert.False(SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = "UDPP_WAREHOUSEITEM",
        }));
        Assert.Single(RootComponentNodes());
    }

    [Fact]
    public void EnsureRootComponent_AddsIdComponent()
    {
        var added = SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.Role,
            Id = RoleId,
        });

        Assert.True(added);
        Assert.False(SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.Role,
            Id = RoleId,
        }));
        Assert.Single(RootComponentNodes());
    }

    [Fact]
    public void EnsureRootComponent_MissingManifest_Throws()
    {
        File.Delete(_solutionXmlPath);
        Assert.ThrowsAny<Exception>(() => SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = "udpp_warehouseitem",
        }));
    }
}
