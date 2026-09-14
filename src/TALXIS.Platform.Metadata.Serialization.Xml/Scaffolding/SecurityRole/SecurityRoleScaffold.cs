using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-security-role template post-action script:
/// registers the rendered role in Solution.xml as a root component (type 20, by id).
/// Pilot of the SolutionRootComponentPatcher consumers - the applier only wires the patcher.
/// </summary>
public static class SecurityRoleScaffold
{
    public static ScaffoldResult Apply(SecurityRoleScaffoldRequest request)
    {
        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.Role,
            Id = Guid.Parse(request.RoleId),
            Behavior = 0,
        });
        return new ScaffoldResult();
    }
}
