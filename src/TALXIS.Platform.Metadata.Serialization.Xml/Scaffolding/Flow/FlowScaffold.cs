using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-flow template post-action script:
/// registers the rendered flow in Solution.xml as a root component (type 29, by id).
/// The old script recovered the id from the rendered .json.data.xml; the interim
/// template passes it as a parameter instead.
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
