using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormSubgridScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-subgrid-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _rowFilePath;

    public FormSubgridScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <forms type="main">
              <systemform>
                <formid>{a1b2c3d4-e5f6-4a1b-8c2d-000000000001}</formid>
                <form>
                  <tabs>
                    <tab>
                      <columns>
                        <column>
                          <sections>
                            <section>
                              <rows>
                                <row>
                                  <cell id="{00000000-0000-0000-0000-000000000010}" />
                                </row>
                              </rows>
                            </section>
                          </sections>
                        </column>
                      </columns>
                    </tab>
                  </tabs>
                </form>
              </systemform>
            </forms>
            """);
        _rowFilePath = Path.Combine(_root, "subgrid.xml");
        File.WriteAllText(_rowFilePath, """
            <row>
                <cell locklevel="0" id="{00000000-0000-0000-0000-000000000020}" rowspan="4" colspan="1" auto="false" showlabel="false">
                    <labels>
                        <label description="Items" languagecode="1033" />
                    </labels>
                    <control indicationOfSubgrid="true" id="subgrid" classid="{E7A81278-8635-4D9E-8D4D-59480B391C5B}" uniqueid="{00000000-0000-0000-0000-000000000030}">
                        <parameters>
                            <TargetEntityType>contact</TargetEntityType>
                            <ViewId>{00000000-0000-0000-0000-000000000040}</ViewId>
                        </parameters>
                    </control>
                </cell>
            </row>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormSubgridScaffoldRequest Request(string formId = FormId) => new()
    {
        SolutionRootPath = _root,
        EntityLogicalName = EntityName,
        FormType = "main",
        FormId = formId,
        RowFilePath = _rowFilePath,
    };

    [Fact]
    public void Apply_MissingFormFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            FormSubgridScaffold.Apply(Request(formId: "00000000-0000-0000-0000-0000000000ff")));
    }

    [Fact]
    public void Apply_AppendsSubgridRowToRowsContainer()
    {
        var result = FormSubgridScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var rows = doc.Descendants("rows").Single().Elements("row").ToList();
        Assert.Equal(2, rows.Count);
        var control = rows[1].Descendants("control").Single();
        Assert.Equal("true", control.Attribute("indicationOfSubgrid")?.Value);
        Assert.Equal("contact", control.Descendants("TargetEntityType").Single().Value);
    }

    [Fact]
    public void Apply_BracedFormId_ResolvesSameFormFile()
    {
        FormSubgridScaffold.Apply(Request(formId: $"{{{FormId}}}"));

        var doc = XDocument.Load(_formFilePath);
        Assert.Equal(2, doc.Descendants("row").Count());
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormSubgrid()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormSubgrid",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["row"] = _rowFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Equal(2, doc.Descendants("row").Count());
    }
}
