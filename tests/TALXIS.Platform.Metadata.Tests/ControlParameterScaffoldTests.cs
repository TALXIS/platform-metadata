using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class ControlParameterScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string UnusedToken = "defaultеtemplateexample";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-control-parameter-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _parametersFilePath;

    public ControlParameterScaffoldTests()
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
                              <cell id="{00000000-0000-0000-0000-000000000010}">
                                <control id="udpp_locationid" classid="{270BD3DB-D9AF-4782-9025-509E298DEC0A}" datafieldname="udpp_locationid" />
                              </cell>
                            </row>
                            <row>
                              <cell id="{00000000-0000-0000-0000-000000000011}" />
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
        _parametersFilePath = Path.Combine(_root, "parameters.xml");
        File.WriteAllText(_parametersFilePath, $$"""
            <parameters>
              <FilterRelationshipName>udpp_location_items</FilterRelationshipName>
              <DependentAttributeName>{{UnusedToken}}</DependentAttributeName>
              <DefaultViewId>{{{UnusedToken}}}</DefaultViewId>
              <AutoResolve>true</AutoResolve>
            </parameters>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ControlParameterScaffoldRequest Request()
    {
        var request = new ControlParameterScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
            FormType = "main",
            FormId = FormId,
            ParametersFilePath = _parametersFilePath,
        };
        request.Placement.RowIndex = "1";
        return request;
    }

    [Fact]
    public void Apply_StripsUnusedNodesAndAppendsParameters()
    {
        var result = ControlParameterScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var parameters = doc.Descendants("control").Single().Element("parameters")!;
        Assert.Equal("udpp_location_items", parameters.Element("FilterRelationshipName")?.Value);
        Assert.Equal("true", parameters.Element("AutoResolve")?.Value);
        Assert.Null(parameters.Element("DependentAttributeName"));
        Assert.Null(parameters.Element("DefaultViewId"));
    }

    [Fact]
    public void Apply_RowWithCellWithoutControl_Throws()
    {
        var request = Request();
        request.Placement.RowIndex = "2";
        var ex = Assert.Throws<InvalidOperationException>(() => ControlParameterScaffold.Apply(request));
        Assert.Contains("Control node not found", ex.Message);
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesControlParameter()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "ControlParameter",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["parameters"] = _parametersFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["row-index"] = "1",
                ["set-to-tab-footer"] = "False",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("parameters"));
    }
}
