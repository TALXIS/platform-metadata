using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class RibbonButtonHideScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-ribbon-hide-scaffold").FullName;
    private readonly string _ribbonPath;
    private readonly string _hidePath;

    public RibbonButtonHideScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Entities", "udpp_warehouseitem"));
        _ribbonPath = Path.Combine(_root, "Entities", "udpp_warehouseitem", "RibbonDiff.xml");
        File.WriteAllText(_ribbonPath, """
            <RibbonDiffXml><CustomActions><CustomAction Id="existing" /></CustomActions></RibbonDiffXml>
            """);

        _hidePath = Path.Combine(_root, "hide.xml");
        File.WriteAllText(_hidePath, """
            <CustomActionsToHide>
              <HideCustomAction HideActionId="udpp.Mscrm.HomepageGrid.udpp_warehouseitem.NewRecord.Hide" Location="Mscrm.HomepageGrid.udpp_warehouseitem.NewRecord" />
            </CustomActionsToHide>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private RibbonButtonHideScaffoldRequest Request() => new()
    {
        SolutionRootPath = _root,
        RibbonDiffFilePath = _ribbonPath,
        HideFilePath = _hidePath,
    };

    [Fact]
    public void Apply_AppendsHideAction()
    {
        RibbonButtonHideScaffold.Apply(Request());

        var customActions = XDocument.Load(_ribbonPath).Descendants("CustomActions").Single();
        Assert.Single(customActions.Elements("CustomAction"));
        var hide = customActions.Elements("HideCustomAction").Single();
        Assert.Equal("Mscrm.HomepageGrid.udpp_warehouseitem.NewRecord", hide.Attribute("Location")?.Value);
    }

    [Fact]
    public void Apply_EmptyPayload_IsNoOp()
    {
        File.WriteAllText(_hidePath, "<CustomActionsToHide></CustomActionsToHide>");
        var before = File.ReadAllBytes(_ribbonPath);

        RibbonButtonHideScaffold.Apply(Request());

        Assert.Equal(before, File.ReadAllBytes(_ribbonPath));
    }

    [Fact]
    public void Apply_MissingRibbon_Throws()
    {
        File.Delete(_ribbonPath);

        Assert.Throws<FileNotFoundException>(() => RibbonButtonHideScaffold.Apply(Request()));
    }
}
