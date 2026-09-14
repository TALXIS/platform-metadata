using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SecurityRoleScaffoldTests : IDisposable
{
    private const string RoleId = "b1b2c3d4-e5f6-4a1b-8c2d-000000000020";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-security-role-scaffold").FullName;
    private readonly string _solutionXmlPath;

    public SecurityRoleScaffoldTests()
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
    public void Apply_RegistersRoleRootComponent()
    {
        SecurityRoleScaffold.Apply(new SecurityRoleScaffoldRequest
        {
            SolutionRootPath = _root,
            RoleId = RoleId,
        });

        var node = RootComponentNodes().Single();
        Assert.Equal("20", node.Attribute("type")?.Value);
        Assert.Contains(RoleId, node.Attribute("id")?.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("0", node.Attribute("behavior")?.Value);
    }

    [Fact]
    public void Apply_SameRole_IsNotDuplicated()
    {
        var request = new SecurityRoleScaffoldRequest { SolutionRootPath = _root, RoleId = RoleId };
        SecurityRoleScaffold.Apply(request);
        SecurityRoleScaffold.Apply(request);

        Assert.Single(RootComponentNodes());
    }

    [Fact]
    public void Dispatcher_ResolvesSecurityRoleAliasAndBracedId()
    {
        ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "SecurityRole",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string> { ["role-id"] = "{" + RoleId + "}" },
        });

        var node = RootComponentNodes().Single();
        Assert.Equal("20", node.Attribute("type")?.Value);
    }
}
