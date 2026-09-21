using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the rendered security role in Solution.xml as a root component
/// (type 20, by id) using SolutionRootComponentPatcher.
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
