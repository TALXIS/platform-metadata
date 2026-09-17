using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the rendered flow in Solution.xml as a root component (type 29, by id).
/// The workflow id is supplied as a parameter.
/// </summary>
public static class FlowScaffold
{
    public static ScaffoldResult Apply(FlowScaffoldRequest request)
    {
        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.Workflow,
            Id = Guid.Parse(request.WorkflowId),
            Behavior = 0,
        });
        return new ScaffoldResult();
    }
}
