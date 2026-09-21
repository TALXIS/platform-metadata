using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class RibbonCommandParameterScaffoldTests : IDisposable
{
    private const string CommandId = "udpp.udpp_warehouseitem.Command.checkstocklevels";
    private const string FunctionName = "Warehouse.check";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-ribbon-parameter-scaffold").FullName;
    private readonly string _ribbonPath;
    private readonly string _parametersPath;

    public RibbonCommandParameterScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Entities", "udpp_warehouseitem"));
        _ribbonPath = Path.Combine(_root, "Entities", "udpp_warehouseitem", "RibbonDiff.xml");
        File.WriteAllText(_ribbonPath, $"""
            <RibbonDiffXml>
              <CommandDefinitions>
                <CommandDefinition Id="{CommandId}">
                  <Actions><JavaScriptFunction FunctionName="{FunctionName}" Library="$webresource:udpp_main.js" /></Actions>
                </CommandDefinition>
              </CommandDefinitions>
            </RibbonDiffXml>
            """);

        _parametersPath = Path.Combine(_root, "parameter.xml");
        File.WriteAllText(_parametersPath, """
            <Parameters>
              <CrmParameter Value="PrimaryControl" />
            </Parameters>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private RibbonCommandParameterScaffoldRequest Request(string commandId = CommandId, string functionName = FunctionName) => new()
    {
        SolutionRootPath = _root,
        RibbonDiffFilePath = _ribbonPath,
        ParametersFilePath = _parametersPath,
        CommandDefinitionId = commandId,
        FunctionName = functionName,
    };

    [Fact]
    public void Apply_AppendsParameterToFunction()
    {
        RibbonCommandParameterScaffold.Apply(Request());

        var function = XDocument.Load(_ribbonPath).Descendants("JavaScriptFunction").Single();
        Assert.Equal("PrimaryControl", function.Element("CrmParameter")?.Attribute("Value")?.Value);
    }

    [Fact]
    public void Apply_UnknownCommand_ThrowsListingAvailable()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RibbonCommandParameterScaffold.Apply(Request(commandId: "nope")));
        Assert.Contains(CommandId, ex.Message);
    }

    [Fact]
    public void Apply_UnknownFunction_ThrowsListingAvailable()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RibbonCommandParameterScaffold.Apply(Request(functionName: "nope")));
        Assert.Contains(FunctionName, ex.Message);
    }

    [Fact]
    public void Apply_EmptyParameters_WarnsAndKeepsRibbon()
    {
        File.WriteAllText(_parametersPath, "<Parameters></Parameters>");
        var before = File.ReadAllBytes(_ribbonPath);

        var result = RibbonCommandParameterScaffold.Apply(Request());

        Assert.Single(result.Warnings);
        Assert.Equal(before, File.ReadAllBytes(_ribbonPath));
    }
}
