using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormEventHandlerScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string DialogFormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000002";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-event-handler-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _dialogFilePath;

    public FormEventHandlerScaffoldTests()
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

        var dialogs = Path.Combine(_root, "Dialogs");
        Directory.CreateDirectory(dialogs);
        _dialogFilePath = Path.Combine(dialogs, $"{{{DialogFormId}}}.xml");
        File.WriteAllText(_dialogFilePath, """
            <Dialog>
              <FormXml>
                <forms type="dialog">
                  <form>
                    <tabs />
                  </form>
                </forms>
              </FormXml>
            </Dialog>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormEventHandlerScaffoldRequest Request(string eventName = "onload") => new()
    {
        SolutionRootPath = _root,
        EntityLogicalName = EntityName,
        FormType = "main",
        FormId = FormId,
        LibraryName = "udpp_forms.js",
        EventName = eventName,
        FunctionName = "onLoadHandler",
    };

    [Fact]
    public void Apply_RegistersLibraryAndHandler()
    {
        var result = FormEventHandlerScaffold.Apply(Request());

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Equal("udpp_forms.js", doc.Descendants("Library").Single().Attribute("name")?.Value);
        var handler = doc.Descendants("Handler").Single();
        Assert.Equal("$webresource:udpp_forms.js", handler.Attribute("libraryName")?.Value);
        Assert.Equal("true", handler.Attribute("passExecutionContext")?.Value);
        Assert.Equal("onload", handler.Parent!.Parent!.Attribute("name")?.Value);
    }

    [Fact]
    public void Apply_SameEvent_AppendsSecondHandlerWithoutDuplicates()
    {
        FormEventHandlerScaffold.Apply(Request());
        var second = Request();
        second.FunctionName = "secondHandler";
        FormEventHandlerScaffold.Apply(second);

        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("Library"));
        Assert.Single(doc.Descendants("event"));
        Assert.Equal(2, doc.Descendants("Handler").Count());
    }

    [Fact]
    public void Apply_OnChange_KeysEventByAttribute()
    {
        var first = Request("onchange");
        first.AttributeName = "udpp_name";
        FormEventHandlerScaffold.Apply(first);
        var second = Request("onchange");
        second.AttributeName = "udpp_sku";
        FormEventHandlerScaffold.Apply(second);

        var doc = XDocument.Load(_formFilePath);
        Assert.Equal(2, doc.Descendants("event").Count());
        Assert.Contains(doc.Descendants("event"), e => e.Attribute("attribute")?.Value == "udpp_sku");
    }

    [Fact]
    public void Apply_DialogForm_SkipsLibraryButAddsHandler()
    {
        var request = Request();
        request.FormType = "Dialog";
        request.FormId = DialogFormId;
        FormEventHandlerScaffold.Apply(request);

        var doc = XDocument.Load(_dialogFilePath);
        Assert.Empty(doc.Descendants("formLibraries"));
        Assert.Single(doc.Descendants("Handler"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormEventHandler()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormEventHandler",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["library-name"] = "udpp_forms.js",
                ["event-name"] = "onsave",
                ["function-name"] = "onSaveHandler",
                ["pass-execution-context"] = "False",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Equal("false", doc.Descendants("Handler").Single().Attribute("passExecutionContext")?.Value);
    }
}
