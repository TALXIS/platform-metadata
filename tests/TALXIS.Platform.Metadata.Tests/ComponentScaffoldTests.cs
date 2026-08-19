using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-component-scaffold").FullName;

    public ComponentScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        Directory.CreateDirectory(Path.Combine(_root, "Entities", EntityName));
        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <ImportExportXml><SolutionManifest><RootComponents /></SolutionManifest></ImportExportXml>
            """);
        File.WriteAllText(Path.Combine(_root, "Entities", EntityName, "Entity.xml"), $"""
            <Entity>
              <EntityInfo>
                <entity Name="{EntityName}">
                  <attributes>
                  </attributes>
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_UnknownComponentType_ThrowsListingSupportedTypes()
    {
        var ex = Assert.Throws<NotSupportedException>(() => ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "Sitemap",
            SolutionRootPath = _root,
        }));
        Assert.Contains("Attribute", ex.Message);
    }

    [Fact]
    public void Apply_MissingRequiredParameter_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "Attribute",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["attribute"] = "x.xml" },
        }));
        Assert.Contains("'entity'", ex.Message);
    }

    [Fact]
    public void Apply_RegistryAlias_ResolvesToRegisteredApplier()
    {
        var ex = Assert.Throws<ArgumentException>(() => ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "Column",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["attribute"] = "x.xml" },
        }));
        Assert.Contains("'entity'", ex.Message);
    }

    [Fact]
    public void Register_CustomComponentType_IsDispatchable()
    {
        ComponentScaffold.Register("SandboxOnlyType", _ => new ScaffoldResult());

        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "sandboxonlytype",
            SolutionRootPath = _root,
        });

        Assert.Empty(result.Warnings);
        Assert.Contains("SandboxOnlyType", ComponentScaffold.SupportedComponentTypes);
    }

    [Fact]
    public void Apply_Attribute_DispatchesToEntityAttributeScaffold()
    {
        var attributeFile = Path.Combine(_root, "attribute.xml");
        File.WriteAllText(attributeFile, """
            <attribute PhysicalName="udpp_alpha">
                <Type>nvarchar</Type>
                <Name>udpp_alpha</Name>
                <LogicalName>udpp_alpha</LogicalName>
            </attribute>
            """);

        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "attribute",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["attribute"] = attributeFile },
            Parameters = new Dictionary<string, string> { ["entity"] = EntityName },
        });

        Assert.Empty(result.Warnings);
        var entityXml = XDocument.Load(Path.Combine(_root, "Entities", EntityName, "Entity.xml"));
        Assert.Single(entityXml.Descendants("attribute"), a => a.Element("LogicalName")?.Value == "udpp_alpha");
    }
}
