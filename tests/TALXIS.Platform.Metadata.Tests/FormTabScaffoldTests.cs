using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormTabScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string TabId = "a1b2c3d4-e5f6-4a1b-8c2d-0000000000e1";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-tab-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _tabGivenIdFilePath;
    private readonly string _tabUnknownIdFilePath;

    public FormTabScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <form>
              <tabs>
                <tab id="{00000000-0000-0000-0000-0000000000a1}" name="generaltab" />
              </tabs>
            </form>
            """);

        _tabGivenIdFilePath = Path.Combine(_root, "tab-given.xml");
        File.WriteAllText(_tabGivenIdFilePath, $$"""
            <tab verticallayout="true" id="{{{TabId}}}" IsUserDefined="1" name="exampletabname" labelid="{00000000-0000-0000-0000-0000000000d1}">
              <labels>
                <label description="Extra Details" languagecode="1033" />
              </labels>
              <columns></columns>
            </tab>
            """);
        _tabUnknownIdFilePath = Path.Combine(_root, "tab-unknown.xml");
        File.WriteAllText(_tabUnknownIdFilePath, """
            <tab verticallayout="true" id="{unknownTabId}" IsUserDefined="1" name="exampletabname" labelid="{00000000-0000-0000-0000-0000000000d2}">
              <labels>
                <label description="General" languagecode="1033" />
              </labels>
              <columns></columns>
            </tab>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormTabScaffoldRequest Request(string fragmentPath) => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        DisplayName = "Extra Details",
        TabFilePath = fragmentPath,
    };

    [Fact]
    public void Apply_GivenTabId_AppendsWithNormalizedName()
    {
        var request = Request(_tabGivenIdFilePath);
        request.TabId = TabId;
        var result = FormTabScaffold.Apply(request);

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var tab = doc.Descendants("tab").Single(t => t.Attribute("id")?.Value == $"{{{TabId}}}");
        Assert.Equal("extradetails", tab.Attribute("name")?.Value);
        Assert.Equal(2, doc.Descendants("tab").Count());
    }

    [Fact]
    public void Apply_UnknownTabId_GeneratesGuid()
    {
        var request = Request(_tabUnknownIdFilePath);
        request.DisplayName = "General";
        FormTabScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var added = doc.Descendants("tab").Single(t => t.Attribute("name")?.Value == "general");
        Assert.True(Guid.TryParse(added.Attribute("id")?.Value.Trim('{', '}'), out _));
    }

    [Fact]
    public void Apply_RemoveDefaultTab_RemovesGeneralTab()
    {
        var request = Request(_tabGivenIdFilePath);
        request.TabId = TabId;
        request.RemoveDefaultTab = true;
        var result = FormTabScaffold.Apply(request);

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.DoesNotContain(doc.Descendants("tab"), t => t.Attribute("name")?.Value == "generaltab");
        Assert.Single(doc.Descendants("tab"));
    }

    [Fact]
    public void Apply_RemoveDefaultTab_MissingGeneralTab_Warns()
    {
        var request = Request(_tabGivenIdFilePath);
        request.TabId = TabId;
        request.RemoveDefaultTab = true;
        FormTabScaffold.Apply(request);

        var request2 = Request(_tabUnknownIdFilePath);
        request2.RemoveDefaultTab = true;
        var result = FormTabScaffold.Apply(request2);

        Assert.Single(result.Warnings, w => w.Contains("generaltab"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_NormalizesUnknownTabIdSentinel()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormTab",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["tab"] = _tabUnknownIdFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["tab-id"] = "unknownTabId",
                ["display-name"] = "General",
                ["remove-default-tab"] = "False",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("tab"), t => t.Attribute("name")?.Value == "general");
    }
}
