using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormCellScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string DialogFormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000002";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-cell-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _dialogFormFilePath;
    private readonly string _cellFilePath;
    private readonly string _dialogCellFilePath;

    public FormCellScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, FormXml());

        var dialogsDirectory = Path.Combine(_root, "Dialogs");
        Directory.CreateDirectory(dialogsDirectory);
        _dialogFormFilePath = Path.Combine(dialogsDirectory, $"{{{DialogFormId}}}.xml");
        File.WriteAllText(_dialogFormFilePath, FormXml());

        _cellFilePath = Path.Combine(_root, "cell.xml");
        File.WriteAllText(_cellFilePath, """
            <cell id="{00000000-0000-0000-0000-000000000010}" labelid="{00000000-0000-0000-0000-000000000011}">
              <labels>
                <label description="Name" languagecode="1033" />
              </labels>
            </cell>
            """);
        _dialogCellFilePath = Path.Combine(_root, "dialogcell.xml");
        File.WriteAllText(_dialogCellFilePath, """
            <cell id="{00000000-0000-0000-0000-000000000020}" showlabel="true">
              <labels>
                <label description="Name" languagecode="1033" />
              </labels>
            </cell>
            """);
    }

    private static string FormXml() => """
        <form>
          <tabs>
            <tab id="{00000000-0000-0000-0000-0000000000a1}">
              <columns>
                <column>
                  <sections>
                    <section id="{00000000-0000-0000-0000-0000000000b1}">
                      <rows>
                        <row name="first" />
                        <row name="second" />
                      </rows>
                    </section>
                  </sections>
                </column>
              </columns>
            </tab>
          </tabs>
        </form>
        """;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_AppendsCellToLastRow()
    {
        var result = FormCellScaffold.Apply(new FormCellScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
            FormType = "main",
            FormId = FormId,
            CellFilePath = _cellFilePath,
            DialogCellFilePath = _dialogCellFilePath,
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var lastRow = doc.Descendants("row").Single(r => r.Attribute("name")?.Value == "second");
        Assert.Equal("{00000000-0000-0000-0000-000000000010}", lastRow.Elements("cell").Single().Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_RowIndex_TargetsThatRow()
    {
        var request = new FormCellScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
            FormType = "main",
            FormId = FormId,
            CellFilePath = _cellFilePath,
        };
        request.Placement.RowIndex = "1";
        FormCellScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var firstRow = doc.Descendants("row").Single(r => r.Attribute("name")?.Value == "first");
        Assert.Single(firstRow.Elements("cell"));
    }

    [Fact]
    public void Apply_DialogForm_UsesDialogFragment()
    {
        FormCellScaffold.Apply(new FormCellScaffoldRequest
        {
            SolutionRootPath = _root,
            EntitySchemaName = EntityName,
            FormType = "dialog",
            FormId = DialogFormId,
            CellFilePath = _cellFilePath,
            DialogCellFilePath = _dialogCellFilePath,
        });

        var doc = XDocument.Load(_dialogFormFilePath);
        Assert.Equal("{00000000-0000-0000-0000-000000000020}",
            doc.Descendants("cell").Single().Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormCell()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormCell",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["cell"] = _cellFilePath, ["dialog-cell"] = _dialogCellFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["row-index"] = "2",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var lastRow = doc.Descendants("row").Single(r => r.Attribute("name")?.Value == "second");
        Assert.Single(lastRow.Elements("cell"));
    }
}
