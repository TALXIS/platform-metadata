using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class PluginAssemblyStepScaffoldTests : IDisposable
{
    private const string StepId = "0f111111-1111-4111-8111-111111111111";
    private const string PluginTypeId = "0a222222-2222-4222-8222-222222222222";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-plugin-step").FullName;
    private readonly string _stepFile;

    public PluginAssemblyStepScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <SolutionManifest>
                <UniqueName>TestSolution</UniqueName>
                <RootComponents>
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);

        // The data.xml sits in a GUID subfolder to exercise the recursive lookup.
        var assemblyDir = Path.Combine(_root, "PluginAssemblies", "0e333333-3333-4333-8333-333333333333");
        Directory.CreateDirectory(assemblyDir);
        File.WriteAllText(Path.Combine(assemblyDir, "Sandbox.Plugins.dll.data.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <PluginAssembly FullName="Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abcdef0123456789" PluginAssemblyId="0e333333-3333-4333-8333-333333333333" CustomizationLevel="1">
              <PluginTypes>
                <PluginType AssemblyQualifiedName="Sandbox.Plugins.WarehousePlugin, Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abcdef0123456789" PluginTypeId="{PluginTypeId}" Name="Sandbox.Plugins.WarehousePlugin" />
              </PluginTypes>
            </PluginAssembly>
            """);

        _stepFile = Path.Combine(_root, "step.xml");
        File.WriteAllText(_stepFile, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <SdkMessageProcessingStep Name="Sandbox.Plugins.WarehousePlugin: Create of udpp_warehouseitem" SdkMessageProcessingStepId="{{{StepId}}}">
              <PluginTypeName>Sandbox.Plugins.WarehousePlugin, Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=__public-key-token__</PluginTypeName>
              <PluginTypeId></PluginTypeId>
              <FilteringAttributes></FilteringAttributes>
            </SdkMessageProcessingStep>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private PluginAssemblyStepScaffoldRequest Request(string filtering = "") => new()
    {
        SolutionRootPath = _root,
        StepFilePath = _stepFile,
        StepId = StepId,
        AssemblyName = "Sandbox.Plugins",
        PluginClassName = "WarehousePlugin",
        FilteringAttributes = filtering,
    };

    [Fact]
    public void Apply_FinalizesAndCopiesStep()
    {
        PluginAssemblyStepScaffold.Apply(Request("{udpp_name, udpp_sku}"));

        var copied = Path.Combine(_root, "SdkMessageProcessingSteps", "{" + StepId + "}.xml");
        Assert.True(File.Exists(copied));
        var doc = XDocument.Load(copied);
        Assert.Equal("udpp_name,udpp_sku", doc.Root!.Element("FilteringAttributes")!.Value);
        Assert.Equal(PluginTypeId, doc.Root.Element("PluginTypeId")!.Value);
        Assert.Contains("PublicKeyToken=abcdef0123456789", doc.Root.Element("PluginTypeName")!.Value);
    }

    [Fact]
    public void Apply_RegistersType92RootComponent()
    {
        PluginAssemblyStepScaffold.Apply(Request());

        var component = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"))
            .Descendants("RootComponent").Single();
        Assert.Equal("92", component.Attribute("type")?.Value);
        Assert.Equal("{" + StepId + "}", component.Attribute("id")?.Value);
        Assert.Equal("0", component.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_EmptyFilteringAttributes_LeavesElementEmpty()
    {
        PluginAssemblyStepScaffold.Apply(Request());

        var doc = XDocument.Load(Path.Combine(_root, "SdkMessageProcessingSteps", "{" + StepId + "}.xml"));
        Assert.Equal("", doc.Root!.Element("FilteringAttributes")!.Value);
    }

    [Fact]
    public void Apply_UnknownPluginClass_Throws()
    {
        var request = Request();
        request.PluginClassName = "NoSuchPlugin";

        var ex = Assert.Throws<InvalidOperationException>(() => PluginAssemblyStepScaffold.Apply(request));
        Assert.Contains("NoSuchPlugin", ex.Message);
    }
}
