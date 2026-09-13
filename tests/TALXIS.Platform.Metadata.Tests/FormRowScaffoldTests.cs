using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormRowScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-row-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _rowFilePath;

    public FormRowScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <forms type="main">
              <systemform>
                <form>
                  <tabs>
                    <tab id="{00000000-0000-0000-0000-0000000000a1}">
                      <tabfooter />
                      <columns>
                        <column>
                          <sections>
                            <section id="{00000000-0000-0000-0000-0000000000b1}">
                              <rows>
                                <row name="existing" />
                              </rows>
                            </section>
                            <section id="{00000000-0000-0000-0000-0000000000b2}" />
                          </sections>
                        </column>
                      </columns>
                    </tab>
                  </tabs>
                </form>
              </systemform>
            </forms>
            """);
        _rowFilePath = Path.Combine(_root, "row.xml");
        File.WriteAllText(_rowFilePath, "<row>\n\n</row>");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormRowScaffoldRequest Request() => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        RowFilePath = _rowFilePath,
    };

    [Fact]
    public void Apply_AppendsRowToLastSectionCreatingRowsContainer()
    {
        var request = Request();
        var result = FormRowScaffold.Apply(request);

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var lastSection = doc.Descendants("section")
            .Single(s => s.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000b2}");
        Assert.Single(lastSection.Elements("rows").Single().Elements("row"));
    }

    [Fact]
    public void Apply_TargetedSection_AppendsToExistingRows()
    {
        var request = Request();
        request.Placement.SectionId = "00000000-0000-0000-0000-0000000000b1";
        FormRowScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var section = doc.Descendants("section")
            .Single(s => s.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000b1}");
        Assert.Equal(2, section.Element("rows")!.Elements("row").Count());
    }

    [Fact]
    public void Apply_TabFooter_AppendsRowsUnderFooter()
    {
        var request = Request();
        request.Placement.SetToTabFooter = true;
        FormRowScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("tabfooter").Single().Element("rows")!.Elements("row"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_NormalizesUnknownSentinels()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormRow",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["row"] = _rowFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = "unknown",
                ["form-type"] = "unknown",
                ["form-id"] = FormId,
                ["tab-id"] = "unknown",
                ["tab-index"] = "unknown",
                ["column-index"] = "unknown",
                ["section-id"] = "unknown",
                ["section-index"] = "unknown",
                ["set-to-tab-footer"] = "False",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var lastSection = doc.Descendants("section")
            .Single(s => s.Attribute("id")?.Value == "{00000000-0000-0000-0000-0000000000b2}");
        Assert.Single(lastSection.Elements("rows").Single().Elements("row"));
    }
}
