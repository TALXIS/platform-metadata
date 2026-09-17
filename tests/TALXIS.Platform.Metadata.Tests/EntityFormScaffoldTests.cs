using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class EntityFormScaffoldTests : IDisposable
{
    private const string FormId = "e1b2c3d4-e5f6-4a1b-8c2d-000000000060";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-entity-form-scaffold").FullName;
    private readonly string _solutionXmlPath;

    public EntityFormScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        _solutionXmlPath = Path.Combine(_root, "Other", "Solution.xml");
        File.WriteAllText(_solutionXmlPath, """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateMainForm()
    {
        var dir = Path.Combine(_root, "Entities", "udpp_warehouseitem", "FormXml", "main");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "mainform.xml");
        File.WriteAllText(path, """
            <forms type="main">
              <systemform>
                <formid>formexampleId</formid>
                <form><tabs /></form>
              </systemform>
            </forms>
            """);
        return path;
    }

    private string CreateDialogForm()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Dialogs"));
        var path = Path.Combine(_root, "Dialogs", "dialogform.xml");
        File.WriteAllText(path, """
            <Dialog>
              <FormId>formexampleId</FormId>
              <UniqueName>dialogexampleuniquename</UniqueName>
              <form><tabs></tabs></form>
            </Dialog>
            """);
        return path;
    }

    private XElement SingleRootComponent() =>
        XDocument.Load(_solutionXmlPath).Descendants("RootComponent").Single();

    [Fact]
    public void Apply_MainForm_StampsIdRenamesAndRegisters()
    {
        var formPath = CreateMainForm();

        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = _root,
            FormType = "main",
            FormFilePath = formPath,
            FormId = FormId,
        });

        var renamed = Path.Combine(Path.GetDirectoryName(formPath)!, "{" + FormId + "}.xml");
        Assert.True(File.Exists(renamed));
        Assert.False(File.Exists(formPath));
        Assert.Equal("{" + FormId + "}", XDocument.Load(renamed).Descendants("formid").Single().Value);

        var rootComponent = SingleRootComponent();
        Assert.Equal("60", rootComponent.Attribute("type")?.Value);
        Assert.Equal("0", rootComponent.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_MainFormWithoutId_GeneratesOne()
    {
        var formPath = CreateMainForm();

        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = _root,
            FormType = "main",
            FormFilePath = formPath,
        });

        var file = Directory.GetFiles(Path.GetDirectoryName(formPath)!).Single();
        var id = Path.GetFileNameWithoutExtension(file).Trim('{', '}');
        Assert.True(Guid.TryParse(id, out _));
        Assert.Equal("{" + id + "}", SingleRootComponent().Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_Dialog_GeneratesUniqueNameAndOmitsBehavior()
    {
        var formPath = CreateDialogForm();

        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = _root,
            FormType = "dialog",
            FormFilePath = formPath,
            FormId = FormId,
            FormName = "Approve Item!",
            EntitySchemaName = "udpp_warehouseitem",
        });

        var renamed = Path.Combine(_root, "Dialogs", "{" + FormId + "}.xml");
        var doc = XDocument.Load(renamed);
        Assert.Equal("{" + FormId + "}", doc.Descendants("FormId").Single().Value);
        Assert.Equal("udpp_approveitemdialog", doc.Descendants("UniqueName").Single().Value);

        var rootComponent = SingleRootComponent();
        Assert.Equal("60", rootComponent.Attribute("type")?.Value);
        Assert.Null(rootComponent.Attribute("behavior"));
    }

    [Fact]
    public void Apply_DialogWithExplicitUniqueName_KeepsIt()
    {
        var formPath = CreateDialogForm();

        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = _root,
            FormType = "dialog",
            FormFilePath = formPath,
            FormId = FormId,
            DialogUniqueName = "udpp_customdialog",
        });

        var renamed = Path.Combine(_root, "Dialogs", "{" + FormId + "}.xml");
        Assert.Equal("udpp_customdialog", XDocument.Load(renamed).Descendants("UniqueName").Single().Value);
    }

    [Fact]
    public void Apply_QuickFormAlreadyNamedById_IsNotDeleted()
    {
        var dir = Path.Combine(_root, "Entities", "udpp_warehouseitem", "FormXml", "quick");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "{" + FormId + "}.xml");
        File.WriteAllText(path, "<forms type=\"quick\"><systemform><formid>x</formid></systemform></forms>");

        EntityFormScaffold.Apply(new EntityFormScaffoldRequest
        {
            SolutionRootPath = _root,
            FormType = "quickCreate",
            FormFilePath = path,
            FormId = FormId,
        });

        Assert.True(File.Exists(path));
        Assert.Equal("{" + FormId + "}", XDocument.Load(path).Descendants("formid").Single().Value);
    }
}
