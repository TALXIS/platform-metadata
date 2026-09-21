using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class WebResourceScaffoldTests : IDisposable
{
    private const string WebResourceId = "b1b2c3d4-e5f6-4a1b-8c2d-000000000061";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-webresource-scaffold").FullName;
    private readonly string _dataXmlPath;
    private readonly string _sourcePath;

    public WebResourceScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        File.WriteAllText(Path.Combine(_root, "Other", "Solution.xml"), """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);

        Directory.CreateDirectory(Path.Combine(_root, "WebResources"));
        _dataXmlPath = Path.Combine(_root, "WebResources", "examplepublisher_fileexamplename.data.xml");
        File.WriteAllText(_dataXmlPath, """
            <WebResource>
              <WebResourceId>{wridexamplecapital}</WebResourceId>
              <Name>udpp_fileexamplename</Name>
              <DisplayName>fileexampledisplayname</DisplayName>
              <WebResourceType>wrtypeexample</WebResourceType>
              <FileName>/WebResources/udpp_fileexamplenamewridexample</FileName>
            </WebResource>
            """);

        _sourcePath = Path.Combine(_root, "main.js");
        File.WriteAllText(_sourcePath, "console.log('hi');");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Apply() => WebResourceScaffold.Apply(new WebResourceScaffoldRequest
    {
        SolutionRootPath = _root,
        SourceFilePath = _sourcePath,
        DataXmlFilePath = _dataXmlPath,
        PublisherPrefix = "udpp",
        WebResourceId = WebResourceId,
    });

    [Fact]
    public void Apply_FinalizesDataXmlCopiesSourceAndRegisters()
    {
        Apply();

        var dataXml = Path.Combine(_root, "WebResources", "udpp_main.js.data.xml");
        Assert.True(File.Exists(dataXml));
        Assert.False(File.Exists(_dataXmlPath));

        var text = File.ReadAllText(dataXml);
        Assert.Contains("{" + WebResourceId.ToUpperInvariant() + "}", text);
        Assert.Contains("<WebResourceType>3</WebResourceType>", text);
        Assert.Contains("<DisplayName>main.js</DisplayName>", text);
        Assert.Contains(WebResourceId, text);

        Assert.True(File.Exists(Path.Combine(_root, "WebResources", "udpp_main.js")));

        var rootComponent = XDocument.Load(Path.Combine(_root, "Other", "Solution.xml")).Descendants("RootComponent").Single();
        Assert.Equal("61", rootComponent.Attribute("type")?.Value);
        Assert.Equal("udpp_main.js", rootComponent.Attribute("schemaName")?.Value);
    }

    [Fact]
    public void Apply_UnknownExtension_GetsTypeZero()
    {
        var source = Path.Combine(_root, "readme.bin");
        File.WriteAllText(source, "x");

        WebResourceScaffold.Apply(new WebResourceScaffoldRequest
        {
            SolutionRootPath = _root,
            SourceFilePath = source,
            DataXmlFilePath = _dataXmlPath,
            PublisherPrefix = "udpp",
            WebResourceId = WebResourceId,
        });

        var text = File.ReadAllText(Path.Combine(_root, "WebResources", "udpp_readme.bin.data.xml"));
        Assert.Contains("<WebResourceType>0</WebResourceType>", text);
    }

    [Fact]
    public void Apply_SameResourceTwice_DoesNotDuplicateRootComponent()
    {
        Apply();
        File.WriteAllText(_dataXmlPath, "<WebResource><WebResourceType>wrtypeexample</WebResourceType></WebResource>");
        Apply();

        Assert.Single(XDocument.Load(Path.Combine(_root, "Other", "Solution.xml")).Descendants("RootComponent"));
    }
}
