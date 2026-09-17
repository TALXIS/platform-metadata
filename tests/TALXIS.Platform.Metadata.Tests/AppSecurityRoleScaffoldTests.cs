using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class AppSecurityRoleScaffoldTests : IDisposable
{
    private const string RoleA = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string RoleB = "b1b2c3d4-e5f6-4a1b-8c2d-000000000002";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-app-security-role-scaffold").FullName;
    private readonly string _appModulePath;

    public AppSecurityRoleScaffoldTests()
    {
        var dir = Path.Combine(_root, "AppModules", "udpp_warehouseapp");
        Directory.CreateDirectory(dir);
        _appModulePath = Path.Combine(dir, "AppModule.xml");
        File.WriteAllText(_appModulePath, """
            <AppModule>
              <AppModuleComponents><AppModuleComponent type="1" schemaName="udpp_warehouseitem" /></AppModuleComponents>
              <AppModuleRoleMaps><Role id="{00000000-0000-0000-0000-000000000009}" /></AppModuleRoleMaps>
            </AppModule>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Apply(string roleIds) => AppSecurityRoleScaffold.Apply(new AppSecurityRoleScaffoldRequest
    {
        SolutionRootPath = _root,
        AppModuleFilePath = _appModulePath,
        RoleIds = roleIds,
    });

    [Fact]
    public void Apply_ReplacesRoleMapsWithBracedIds()
    {
        Apply($"[{{{RoleA}}}, \"{RoleB}\"]");

        var doc = XDocument.Load(_appModulePath);
        var roleMaps = doc.Descendants("AppModuleRoleMaps").Single();
        Assert.Equal(new[] { "{" + RoleA + "}", "{" + RoleB + "}" },
            roleMaps.Elements("Role").Select(r => r.Attribute("id")?.Value).ToArray());
        Assert.Single(doc.Descendants("AppModuleComponent"));
    }

    [Fact]
    public void Apply_InvalidGuid_ThrowsAndKeepsExistingRoles()
    {
        Assert.Throws<ArgumentException>(() => Apply("[not-a-guid]"));

        var roleMaps = XDocument.Load(_appModulePath).Descendants("AppModuleRoleMaps").Single();
        Assert.Equal("{00000000-0000-0000-0000-000000000009}", roleMaps.Elements("Role").Single().Attribute("id")?.Value);
    }

    [Fact]
    public void Apply_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() => Apply("[]"));
    }
}
