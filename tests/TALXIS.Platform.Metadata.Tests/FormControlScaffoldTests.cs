using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormControlScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-control-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _controlFilePath;

    public FormControlScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <form>
              <tabs>
                <tab id="{00000000-0000-0000-0000-0000000000a1}">
                  <columns>
                    <column>
                      <sections>
                        <section id="{00000000-0000-0000-0000-0000000000b1}">
                          <rows>
                            <row>
                              <cell id="{00000000-0000-0000-0000-000000000010}" />
                            </row>
                            <row name="crowded">
                              <cell id="{00000000-0000-0000-0000-000000000011}" />
                              <cell id="{00000000-0000-0000-0000-000000000012}" />
                            </row>
                            <row>
                              <cell id="{00000000-0000-0000-0000-000000000013}" />
                            </row>
                          </rows>
                        </section>
                      </sections>
                    </column>
                  </columns>
                </tab>
              </tabs>
            </form>
            """);
        _controlFilePath = Path.Combine(_root, "control.xml");
        File.WriteAllText(_controlFilePath, """
            <control id="udpp_name" classid="{4273EDBD-AC1D-40d3-9FB2-095C621B552D}" datafieldname="udpp_name" uniqueid="{00000000-0000-0000-0000-000000000030}" />
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormControlScaffoldRequest Request() => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        ControlFilePath = _controlFilePath,
    };

    [Fact]
    public void Apply_AppendsControlIntoSingleCellOfLastRow()
    {
        var result = FormControlScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var cell = doc.Descendants("cell").Single(c => c.Attribute("id")?.Value == "{00000000-0000-0000-0000-000000000013}");
        Assert.Equal("udpp_name", cell.Elements("control").Single().Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_SubgridSpans_MarkTheCell()
    {
        var request = Request();
        request.Placement.RowIndex = "1";
        request.RowSpan = "8";
        request.ColumnSpan = "2";
        FormControlScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var cell = doc.Descendants("cell").Single(c => c.Attribute("id")?.Value == "{00000000-0000-0000-0000-000000000010}");
        Assert.Equal("8", cell.Attribute("rowspan")?.Value);
        Assert.Equal("2", cell.Attribute("colspan")?.Value);
        Assert.Equal("false", cell.Attribute("auto")?.Value);
    }

    [Fact]
    public void Apply_RowWithMultipleCells_Throws()
    {
        var request = Request();
        request.Placement.RowIndex = "2";
        var ex = Assert.Throws<InvalidOperationException>(() => FormControlScaffold.Apply(request));
        Assert.Contains("Multiple cells", ex.Message);
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormControl()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormControl",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["control"] = _controlFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["row-span"] = "unknown",
                ["col-span"] = "unknown",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var cell = doc.Descendants("cell").Single(c => c.Attribute("id")?.Value == "{00000000-0000-0000-0000-000000000013}");
        Assert.Single(cell.Elements("control"));
        Assert.Null(cell.Attribute("rowspan"));
    }
}
