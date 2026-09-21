using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class PluginAssemblyScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-plugin-assembly").FullName;

    public PluginAssemblyScaffoldTests()
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
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void BuildDataXml_EmitsPluginTypesAndSkipsPluginBase()
    {
        var doc = PluginAssemblyScaffold.BuildDataXml(
            "Sandbox.Plugins", "1.0.0.0", "abcdef0123456789", "0e111111-1111-4111-8111-111111111111",
            "Sandbox.Plugins", new[] { "Sandbox.Plugins.PluginBase", "Sandbox.Plugins.WarehousePlugin" });

        var root = doc.DocumentElement!;
        Assert.Equal("PluginAssembly", root.Name);
        Assert.Equal("Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abcdef0123456789", root.GetAttribute("FullName"));
        Assert.Equal("0e111111-1111-4111-8111-111111111111", root.GetAttribute("PluginAssemblyId"));
        Assert.Equal("2", root.SelectSingleNode("IsolationMode")!.InnerText);
        Assert.Equal("0", root.SelectSingleNode("SourceType")!.InnerText);
        Assert.Equal("/PluginAssemblies/Sandbox.Plugins.dll", root.SelectSingleNode("FileName")!.InnerText);

        var pluginTypes = root.SelectNodes("PluginTypes/PluginType")!.Cast<XmlElement>().ToList();
        var pluginType = Assert.Single(pluginTypes);
        Assert.Equal("Sandbox.Plugins.WarehousePlugin", pluginType.GetAttribute("Name"));
        Assert.StartsWith("Sandbox.Plugins.WarehousePlugin, Sandbox.Plugins, Version=1.0.0.0", pluginType.GetAttribute("AssemblyQualifiedName"));
        Assert.True(Guid.TryParse(pluginType.GetAttribute("PluginTypeId"), out _));
        Assert.True(Guid.TryParse(pluginType.SelectSingleNode("FriendlyName")!.InnerText, out _));
    }

    [Fact]
    public void DualIdentityRootComponent_EmitsIdBeforeSchemaName()
    {
        SolutionRootComponentPatcher.EnsureRootComponent(_root, new RootComponent
        {
            Type = ComponentType.PluginAssembly,
            Id = Guid.Parse("0e111111-1111-4111-8111-111111111111"),
            SchemaName = "Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abcdef0123456789",
            Behavior = 0,
        });

        var content = File.ReadAllText(Path.Combine(_root, "Other", "Solution.xml"));
        Assert.Contains(
            "<RootComponent type=\"91\" id=\"{0e111111-1111-4111-8111-111111111111}\" schemaName=\"Sandbox.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=abcdef0123456789\" behavior=\"0\" />",
            content);

        var component = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml"))
            .Descendants("RootComponent").Single();
        Assert.Equal(["type", "id", "schemaName", "behavior"], component.Attributes().Select(a => a.Name.LocalName).ToArray());
    }
}
