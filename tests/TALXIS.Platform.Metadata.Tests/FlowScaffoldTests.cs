using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FlowScaffoldTests : IDisposable
{
    private const string WorkflowId = "d1b2c3d4-e5f6-4a1b-8c2d-000000000029";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-flow-scaffold").FullName;
    private readonly string _solutionXmlPath;

    public FlowScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        _solutionXmlPath = Path.Combine(_root, "Other", "Solution.xml");
        File.WriteAllText(_solutionXmlPath, """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IEnumerable<XElement> RootComponentNodes() =>
        XDocument.Load(_solutionXmlPath).Descendants("RootComponent");

    [Fact]
    public void Apply_RegistersWorkflowRootComponent()
    {
        FlowScaffold.Apply(new FlowScaffoldRequest
        {
            SolutionRootPath = _root,
            WorkflowId = WorkflowId,
        });

        var node = RootComponentNodes().Single();
        Assert.Equal("29", node.Attribute("type")?.Value);
        Assert.Contains(WorkflowId, node.Attribute("id")?.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("0", node.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_SameWorkflow_IsNotDuplicated()
    {
        var request = new FlowScaffoldRequest { SolutionRootPath = _root, WorkflowId = WorkflowId };
        FlowScaffold.Apply(request);
        FlowScaffold.Apply(request);

        Assert.Single(RootComponentNodes());
    }

    [Fact]
    public void Dispatcher_ResolvesFlowAliasAndBracedId()
    {
        ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "Flow",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string> { ["workflow-id"] = "{" + WorkflowId + "}" },
        });

        var node = RootComponentNodes().Single();
        Assert.Equal("29", node.Attribute("type")?.Value);
    }
}
