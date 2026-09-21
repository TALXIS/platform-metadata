using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EntityScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string QuickCreateFormId = "0a111111-1111-4111-8111-111111111111";
    private const string MainFormId = "0b222222-2222-4222-8222-222222222222";
    private const string CardFormId = "0c333333-3333-4333-8333-333333333333";
    private const string QuickFormId = "0d444444-4444-4444-8444-444444444444";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-entity-scaffold").FullName;

    public EntityScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        Directory.CreateDirectory(Path.Combine(_root, "Entities", EntityName));

        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SolutionManifest>
                <UniqueName>TestSolution</UniqueName>
                <LocalizedNames xsi:nil="true">
                </LocalizedNames>
                <RootComponents>
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);

        File.WriteAllText(Path.Combine(_root, "Entities", EntityName, "Entity.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Name LocalizedName="Warehouse Item" OriginalName="Warehouse Item">{EntityName}</Name>
              <EntityInfo>
                <entity Name="{EntityName}">
                  <attributes>
                    <attribute PhysicalName="udpp_Zebra">
                      <Type>nvarchar</Type>
                      <LogicalName>udpp_zebra</LogicalName>
                    </attribute>
                    <attribute PhysicalName="udpp_alpha">
                      <Type>nvarchar</Type>
                      <LogicalName>udpp_alpha</LogicalName>
                    </attribute>
                  </attributes>
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private EntityScaffoldRequest Request(int behavior = 0) => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        Behavior = behavior,
    };

    private List<XElement> RootComponents() =>
        XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"))
            .Descendants("RootComponent").ToList();

    [Fact]
    public void Apply_RegistersEntityAndFormComponentsInOldOrder()
    {
        var request = Request();
        request.QuickCreateFormId = QuickCreateFormId;
        request.MainFormId = MainFormId;
        request.CardFormId = CardFormId;
        request.QuickFormId = QuickFormId;

        EntityScaffold.Apply(request);

        var components = RootComponents();
        Assert.Equal(5, components.Count);
        Assert.Equal("1", components[0].Attribute("type")?.Value);
        Assert.Equal(EntityName, components[0].Attribute("schemaName")?.Value);
        Assert.Equal("0", components[0].Attribute("behavior")?.Value);
        var formIds = new[] { QuickCreateFormId, MainFormId, CardFormId, QuickFormId };
        for (var i = 0; i < formIds.Length; i++)
        {
            Assert.Equal("60", components[i + 1].Attribute("type")?.Value);
            Assert.Equal("{" + formIds[i] + "}", components[i + 1].Attribute("id")?.Value);
            Assert.Equal("0", components[i + 1].Attribute("behavior")?.Value);
        }
    }

    [Fact]
    public void Apply_ExistingReference_RegistersOnlyEntityWithBehavior2()
    {
        EntityScaffold.Apply(Request(behavior: 2));

        var components = RootComponents();
        var component = Assert.Single(components);
        Assert.Equal("1", component.Attribute("type")?.Value);
        Assert.Equal("2", component.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_SortsEntityAttributesCaseInsensitive()
    {
        EntityScaffold.Apply(Request());

        var physicalNames = XDocument.Load(Path.Combine(_root, "Entities", EntityName, "Entity.xml"))
            .Descendants("attribute").Select(a => a.Attribute("PhysicalName")?.Value).ToList();
        Assert.Equal(["udpp_alpha", "udpp_Zebra"], physicalNames);
    }

    [Fact]
    public void Apply_CollapsesNilTagsInSolutionXml()
    {
        EntityScaffold.Apply(Request());

        var content = File.ReadAllText(Path.Combine(_root, "Other", "Solution.xml"));
        Assert.Contains("xsi:nil=\"true\"></LocalizedNames>", content);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        var request = Request();
        request.MainFormId = MainFormId;

        EntityScaffold.Apply(request);
        EntityScaffold.Apply(request);

        Assert.Equal(2, RootComponents().Count);
    }
}
