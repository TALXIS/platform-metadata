using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class RibbonButtonScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-ribbon-button-scaffold").FullName;
    private readonly string _ribbonPath;
    private readonly RibbonButtonScaffoldRequest _request;

    public RibbonButtonScaffoldTests()
    {
        _ribbonPath = Path.Combine(_root, "Entities", "udpp_warehouseitem", "RibbonDiff.xml");

        File.WriteAllText(Path.Combine(_root, "empty-ribbon.xml"), """
            <RibbonDiffXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <CustomActions />
              <CommandDefinitions />
              <LocLabels />
            </RibbonDiffXml>
            """);
        File.WriteAllText(Path.Combine(_root, "command-definition.xml"), """
            <CommandDefinitions>
              <CommandDefinition Id="udpp.udpp_warehouseitem.Command.__button-logical-name__">
                <Actions><JavaScriptFunction FunctionName="Warehouse.check" Library="$webresource:udpp_main.js" /></Actions>
              </CommandDefinition>
            </CommandDefinitions>
            """);
        File.WriteAllText(Path.Combine(_root, "loc-labels.xml"), """
            <LocLabels>
              <LocLabel Id="udpp.udpp_warehouseitem.Button.__button-logical-name__.LabelText">
                <Titles><Title languagecode="1033" description="Check" /></Titles>
              </LocLabel>
            </LocLabels>
            """);
        File.WriteAllText(Path.Combine(_root, "custom-action.xml"), """
            <CustomActions>
              <CustomAction Id="udpp.udpp_warehouseitem.Button.__button-logical-name__.CustomAction" Location="Mscrm.Form.udpp_warehouseitem.MainTab">
                <CommandUIDefinition>
                  <Button Id="udpp.udpp_warehouseitem.Button.__button-logical-name__" __icon-16x16-placeholder__ __icon-32x32-placeholder__ __modern-image-placeholder__ Sequence="31" />
                </CommandUIDefinition>
              </CustomAction>
            </CustomActions>
            """);

        _request = new RibbonButtonScaffoldRequest
        {
            SolutionRootPath = _root,
            RibbonDiffFilePath = _ribbonPath,
            EmptyRibbonFilePath = Path.Combine(_root, "empty-ribbon.xml"),
            CommandDefinitionFilePath = Path.Combine(_root, "command-definition.xml"),
            LocLabelsFilePath = Path.Combine(_root, "loc-labels.xml"),
            CustomActionFilePath = Path.Combine(_root, "custom-action.xml"),
            ButtonLabel = "Check Stock Levels",
        };
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_CreatesRibbonAndMergesPayloadsWithLogicalName()
    {
        RibbonButtonScaffold.Apply(_request);

        var doc = XDocument.Load(_ribbonPath);
        Assert.Equal("udpp.udpp_warehouseitem.Command.checkstocklevels",
            doc.Descendants("CommandDefinition").Single().Attribute("Id")?.Value);
        Assert.Single(doc.Descendants("LocLabel"));
        var button = doc.Descendants("Button").Single();
        Assert.Equal("udpp.udpp_warehouseitem.Button.checkstocklevels", button.Attribute("Id")?.Value);
        Assert.Null(button.Attribute("Image16by16"));
        Assert.Null(button.Attribute("ModernImage"));
    }

    [Fact]
    public void Apply_WithIcons_EmitsWebResourceAttributes()
    {
        _request.Image16by16 = "udpp_icon16.png";
        _request.Image32by32 = "udpp_icon32.png";
        _request.ModernImage = "udpp_modern.svg";

        RibbonButtonScaffold.Apply(_request);

        var button = XDocument.Load(_ribbonPath).Descendants("Button").Single();
        Assert.Equal("$webresource:udpp_icon16.png", button.Attribute("Image16by16")?.Value);
        Assert.Equal("$webresource:udpp_icon32.png", button.Attribute("Image32by32")?.Value);
        Assert.Equal("$webresource:udpp_modern.svg", button.Attribute("ModernImage")?.Value);
    }

    [Fact]
    public void Apply_SentinelIcons_MeanNone()
    {
        _request.Image16by16 = "icon16pathdefault";
        _request.Image32by32 = "icon32pathdefault";
        _request.ModernImage = "modernimagedefault";

        RibbonButtonScaffold.Apply(_request);

        var button = XDocument.Load(_ribbonPath).Descendants("Button").Single();
        Assert.Null(button.Attribute("Image16by16"));
        Assert.Null(button.Attribute("Image32by32"));
        Assert.Null(button.Attribute("ModernImage"));
    }

    [Fact]
    public void Apply_ExistingRibbon_AppendsSecondButton()
    {
        RibbonButtonScaffold.Apply(_request);
        _request.ButtonLabel = "Fix Item's Stock & Bins!";

        RibbonButtonScaffold.Apply(_request);

        var doc = XDocument.Load(_ribbonPath);
        Assert.Equal(2, doc.Descendants("CustomAction").Count());
        Assert.Contains("fixitemsstockbins",
            doc.Descendants("CommandDefinition").Select(c => c.Attribute("Id")?.Value).Last());
    }

    [Fact]
    public void DeriveLogicalName_StripsPunctuationSymbolsAndSpaces()
    {
        Assert.Equal("fixitemsstockbins", RibbonButtonScaffold.DeriveLogicalName("Fix Item's Stock & Bins!"));
    }

    [Fact]
    public void Apply_RawAmpersandInRenderedPayload_IsEscaped()
    {
        File.WriteAllText(Path.Combine(_root, "loc-labels.xml"), """
            <LocLabels>
              <LocLabel Id="udpp.udpp_warehouseitem.Button.__button-logical-name__.LabelText">
                <Titles><Title languagecode="1033" description="Fix Item's Stock & Bins!" /></Titles>
              </LocLabel>
            </LocLabels>
            """);

        RibbonButtonScaffold.Apply(_request);

        var title = XDocument.Load(_ribbonPath).Descendants("Title").Single();
        Assert.Equal("Fix Item's Stock & Bins!", title.Attribute("description")?.Value);
    }
}
