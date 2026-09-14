using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormParameterScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-parameter-scaffold").FullName;
    private readonly string _formFilePath;

    public FormParameterScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <forms type="main">
              <systemform>
                <form>
                  <tabs />
                </form>
              </systemform>
            </forms>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormParameterScaffoldRequest Request(string name = "record_id", string type = "SafeString") => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        ParameterName = name,
        ParameterType = type,
    };

    [Fact]
    public void Apply_CreatesContainerAndParameter()
    {
        var result = FormParameterScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var parameter = doc.Descendants("formparameters").Single().Elements("querystringparameter").Single();
        Assert.Equal("record_id", parameter.Attribute("name")?.Value);
        Assert.Equal("SafeString", parameter.Attribute("type")?.Value);
    }

    [Fact]
    public void Apply_ExistingParameter_IsNotDuplicated()
    {
        FormParameterScaffold.Apply(Request());
        FormParameterScaffold.Apply(Request(type: "Integer"));

        var doc = XDocument.Load(_formFilePath);
        var parameter = doc.Descendants("querystringparameter").Single();
        Assert.Equal("SafeString", parameter.Attribute("type")?.Value);
    }

    [Fact]
    public void Apply_SecondParameter_AppendsToExistingContainer()
    {
        FormParameterScaffold.Apply(Request());
        FormParameterScaffold.Apply(Request(name: "mode", type: "Integer"));

        var doc = XDocument.Load(_formFilePath);
        Assert.Equal(2, doc.Descendants("querystringparameter").Count());
        Assert.Single(doc.Descendants("formparameters"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormParameter()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormParameter",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["parameter-name"] = "record_id",
                ["parameter-type"] = "UniqueId",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Equal("UniqueId", doc.Descendants("querystringparameter").Single().Attribute("type")?.Value);
    }
}
