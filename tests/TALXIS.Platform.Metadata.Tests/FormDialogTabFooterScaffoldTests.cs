using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormDialogTabFooterScaffoldTests : IDisposable
{
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string FooterId = "a1b2c3d4-e5f6-4a1b-8c2d-0000000000f1";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-dialog-tabfooter-scaffold").FullName;
    private readonly string _dialogFilePath;

    public FormDialogTabFooterScaffoldTests()
    {
        var dialogs = Path.Combine(_root, "Dialogs");
        Directory.CreateDirectory(dialogs);
        _dialogFilePath = Path.Combine(dialogs, $"{{{FormId}}}.xml");
        File.WriteAllText(_dialogFilePath, """
            <Dialog>
              <FormXml>
                <forms type="dialog">
                  <form>
                    <tabs>
                      <tab id="{00000000-0000-0000-0000-0000000000a1}" name="first" />
                      <tab id="{00000000-0000-0000-0000-0000000000a2}" name="second" />
                    </tabs>
                  </form>
                </forms>
              </FormXml>
            </Dialog>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_GivenFooterId_AppendsToLastTab()
    {
        var request = new FormDialogTabFooterScaffoldRequest
        {
            SolutionRootPath = _root,
            FormId = FormId,
            TabFooterId = FooterId,
        };
        var result = FormDialogTabFooterScaffold.Apply(request);

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_dialogFilePath);
        var lastTab = doc.Descendants("tab").Single(t => t.Attribute("name")?.Value == "second");
        Assert.Equal($"{{{FooterId}}}", lastTab.Element("tabfooter")?.Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_TabIndex_TargetsThatTab_AndGeneratesFooterId()
    {
        var request = new FormDialogTabFooterScaffoldRequest
        {
            SolutionRootPath = _root,
            FormId = FormId,
        };
        request.Placement.TabIndex = "1";
        FormDialogTabFooterScaffold.Apply(request);

        var doc = XDocument.Load(_dialogFilePath);
        var firstTab = doc.Descendants("tab").Single(t => t.Attribute("name")?.Value == "first");
        Assert.True(Guid.TryParse(firstTab.Element("tabfooter")?.Attribute("id")?.Value.Trim('{', '}'), out _));
    }

    [Fact]
    public void Apply_NoFormId_PicksNewestDialog()
    {
        FormDialogTabFooterScaffold.Apply(new FormDialogTabFooterScaffoldRequest { SolutionRootPath = _root });

        var doc = XDocument.Load(_dialogFilePath);
        Assert.Single(doc.Descendants("tabfooter"));
    }

    [Fact]
    public void Apply_MissingDialog_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => FormDialogTabFooterScaffold.Apply(new FormDialogTabFooterScaffoldRequest
        {
            SolutionRootPath = _root,
            FormId = "00000000-0000-0000-0000-0000000000ff",
        }));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormDialogTabFooter()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormDialogTabFooter",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string>
            {
                ["form-id"] = FormId,
                ["tab-id"] = "unknown",
                ["tab-index"] = "unknown",
                ["tab-footer-id"] = FooterId,
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_dialogFilePath);
        Assert.Single(doc.Descendants("tabfooter"));
    }
}
