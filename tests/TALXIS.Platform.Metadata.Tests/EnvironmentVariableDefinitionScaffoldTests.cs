using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EnvironmentVariableDefinitionScaffoldTests : IDisposable
{
    private const string SchemaName = "udpp_apiurl";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-envvar-definition-scaffold").FullName;
    private readonly string _definitionPath;

    public EnvironmentVariableDefinitionScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);

        var dir = Path.Combine(_root, "environmentvariabledefinitions", SchemaName);
        Directory.CreateDirectory(dir);
        _definitionPath = Path.Combine(dir, "environmentvariabledefinition.xml");
        File.WriteAllText(_definitionPath, """
            <environmentvariabledefinition schemaname="udpp_apiurl">
              <displayname default="Api Url" />
              <description default="" />
              <defaultvalue></defaultvalue>
              <type>100000000</type>
            </environmentvariabledefinition>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ScaffoldResult Apply() => EnvironmentVariableDefinitionScaffold.Apply(new EnvironmentVariableDefinitionScaffoldRequest
    {
        SolutionRootPath = _root,
        DefinitionFilePath = _definitionPath,
        SchemaName = SchemaName,
    });

    [Fact]
    public void Apply_RemovesBlankNodesAndRegisters()
    {
        Apply();

        var doc = XDocument.Load(_definitionPath);
        Assert.Null(doc.Root!.Element("defaultvalue"));
        Assert.Null(doc.Root!.Element("description"));

        var rootComponent = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml")).Descendants("RootComponent").Single();
        Assert.Equal("380", rootComponent.Attribute("type")?.Value);
        Assert.Equal(SchemaName, rootComponent.Attribute("schemaName")?.Value);
    }

    [Fact]
    public void Apply_KeepsFilledNodes()
    {
        File.WriteAllText(_definitionPath, """
            <environmentvariabledefinition schemaname="udpp_apiurl">
              <description default="An url" />
              <defaultvalue>https://example.test</defaultvalue>
            </environmentvariabledefinition>
            """);

        Apply();

        var doc = XDocument.Load(_definitionPath);
        Assert.Equal("https://example.test", doc.Root!.Element("defaultvalue")?.Value);
        Assert.Equal("An url", doc.Root!.Element("description")?.Attribute("default")?.Value);
    }

    [Fact]
    public void Apply_WithoutSolutionXml_SkipsRegistrationWithWarning()
    {
        File.Delete(Path.Combine(_root, "Other", "Solution.xml"));

        var result = Apply();

        Assert.Single(result.Warnings);
    }
}
