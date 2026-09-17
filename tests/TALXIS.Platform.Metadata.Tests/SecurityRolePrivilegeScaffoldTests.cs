using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class SecurityRolePrivilegeScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-security-role-privilege-scaffold").FullName;
    private readonly string _rolePath;

    public SecurityRolePrivilegeScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Roles"));
        _rolePath = Path.Combine(_root, "Roles", "Warehouse worker.xml");
        File.WriteAllText(_rolePath, """
            <Role id="{c1b2c3d4-e5f6-4a1b-8c2d-000000000002}" name="Warehouse worker" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <IsCustomizable>1</IsCustomizable>
              <RolePrivileges>
              </RolePrivileges>
            </Role>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Apply(string privileges) => SecurityRolePrivilegeScaffold.Apply(new SecurityRolePrivilegeScaffoldRequest
    {
        SolutionRootPath = _root,
        RoleFilePath = _rolePath,
        EntityLogicalName = "udpp_warehouseitem",
        Privileges = privileges,
    });

    private List<(string? Name, string? Level)> Privileges() =>
        XDocument.Load(_rolePath).Descendants("RolePrivilege")
            .Select(p => (p.Attribute("name")?.Value, p.Attribute("level")?.Value))
            .ToList();

    [Fact]
    public void Apply_QuotedJson_AddsPrivilegesSorted()
    {
        Apply("""[{"privilegetype":"Read","level":"Global"},{"privilegetype":"Create","level":"User"}]""");

        Assert.Equal(new[]
        {
            ("prvCreateudpp_warehouseitem", "Basic"),
            ("prvReadudpp_warehouseitem", "Global"),
        }, Privileges().Select(p => (p.Name!, p.Level!)).ToArray());
    }

    [Fact]
    public void Apply_BareJsonAndTypeAlias_Work()
    {
        Apply("[{type: Write, level: BusinessUnit},{privilegetype: Delete, level: ParentChild}]");

        Assert.Equal(new[]
        {
            ("prvDeleteudpp_warehouseitem", "Deep"),
            ("prvWriteudpp_warehouseitem", "Local"),
        }, Privileges().Select(p => (p.Name!, p.Level!)).ToArray());
    }

    [Fact]
    public void Apply_NoneLevel_OmitsThePrivilege()
    {
        Apply("[{privilegetype: Read, level: Global},{privilegetype: Delete, level: None}]");

        Assert.Single(Privileges());
    }

    [Fact]
    public void Apply_ExistingPrivilege_UpdatesLevel()
    {
        Apply("[{privilegetype: Read, level: Basic}]");
        Apply("[{privilegetype: Read, level: Global}]");

        var privilege = Assert.Single(Privileges());
        Assert.Equal("Global", privilege.Level);
    }

    [Theory]
    [InlineData("[{privilegetype: Fly, level: Global}]", "Unknown privilege type")]
    [InlineData("[{privilegetype: Read, level: Everywhere}]", "Unknown privilege level")]
    [InlineData("[{level: Global}]", "has no 'privilegetype'")]
    [InlineData("[]", "did not contain any privileges")]
    public void Apply_InvalidSpec_ThrowsNamingTheProblem(string spec, string expectedMessagePart)
    {
        var ex = Assert.Throws<ArgumentException>(() => Apply(spec));
        Assert.Contains(expectedMessagePart, ex.Message);
    }
}
