using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormColumnScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-column-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _columnFilePath;

    public FormColumnScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <form>
              <tabs>
                <tab id="{00000000-0000-0000-0000-0000000000a1}">
                  <tabfooter />
                  <columns>
                    <column width="50%" />
                  </columns>
                </tab>
                <tab id="{00000000-0000-0000-0000-0000000000a2}" />
              </tabs>
            </form>
            """);
        _columnFilePath = Path.Combine(_root, "column.xml");
        File.WriteAllText(_columnFilePath, """
            <column width="100%">
              <sections>

              </sections>
            </column>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormColumnScaffoldRequest Request() => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        ColumnFilePath = _columnFilePath,
    };

    [Fact]
    public void Apply_LastTabWithoutColumns_CreatesContainerAndAppends()
    {
        var result = FormColumnScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var lastTab = doc.Descendants("tab").Single(t => t.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000a2}");
        Assert.Equal("100%", lastTab.Element("columns")!.Elements("column").Single().Attribute("width")?.Value);
    }

    [Fact]
    public void Apply_TargetedTab_AppendsToExistingColumns()
    {
        var request = Request();
        request.Placement.TabId = "00000000-0000-0000-0000-0000000000a1";
        FormColumnScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var tab = doc.Descendants("tab").Single(t => t.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000a1}");
        Assert.Equal(2, tab.Element("columns")!.Elements("column").Count());
    }

    [Fact]
    public void Apply_TabFooter_UsesDocumentWideFooter()
    {
        var request = Request();
        request.Placement.TabIndex = "2";
        request.Placement.SetToTabFooter = true;
        FormColumnScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("tabfooter").Single().Element("columns")!.Elements("column"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormColumn()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormColumn",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["column"] = _columnFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["tab-id"] = "unknown",
                ["tab-index"] = "1",
                ["set-to-tab-footer"] = "False",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var tab = doc.Descendants("tab").Single(t => t.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000a1}");
        Assert.Equal(2, tab.Element("columns")!.Elements("column").Count());
    }
}
