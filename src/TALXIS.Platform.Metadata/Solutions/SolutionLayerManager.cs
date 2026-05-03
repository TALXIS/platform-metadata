namespace TALXIS.Platform.Metadata.Solutions;

using TALXIS.Platform.Metadata.Merging;

/// <summary>
/// Manages component layers across a workspace.
/// Maps component identity (type + id) to its layer stack.
/// </summary>
public sealed class SolutionLayerManager
{
    private readonly Dictionary<string, LayerStack> _stacks = new();
    private readonly Dictionary<ComponentType, IComponentMerger> _mergers = new();

    /// <summary>
    /// Creates a layer manager preconfigured with the built-in mergers for mergeable component types.
    /// </summary>
    public SolutionLayerManager()
    {
        RegisterMerger(new FormMerger());
        RegisterMerger(new SiteMapMerger());
        RegisterMerger(new AppModuleMerger());
        RegisterMerger(new RibbonMerger());
    }

    /// <summary>
    /// Gets an existing layer stack for the component or creates a new one.
    /// </summary>
    public LayerStack GetOrCreateStack(ComponentType componentType, string componentId)
    {
        var key = $"{(int)componentType}:{componentId}";
        if (!_stacks.TryGetValue(key, out var stack))
        {
            stack = new LayerStack
            {
                ComponentType = componentType,
                ComponentId = componentId,
                RequiresMerge = ComponentDefinitionRegistry.GetByType(componentType)?.IsMergeable == true
            };
            _stacks[key] = stack;
        }
        return stack;
    }

    /// <summary>
    /// Finds an existing layer stack without creating a new one.
    /// </summary>
    public LayerStack? FindStack(ComponentType componentType, string componentId)
    {
        var key = $"{(int)componentType}:{componentId}";
        return _stacks.TryGetValue(key, out var stack) ? stack : null;
    }

    /// <summary>
    /// Gets all tracked component stacks.
    /// </summary>
    public IReadOnlyCollection<LayerStack> AllStacks => _stacks.Values;

    /// <summary>
    /// Registers or replaces the merger used for a mergeable component type.
    /// </summary>
    public void RegisterMerger(IComponentMerger merger) => _mergers[merger.ComponentType] = merger;

    /// <summary>
    /// Imports a solution's components as a new layer.
    /// Each component gets a layer with the given solution name and order.
    /// </summary>
    public void ImportSolutionLayer(string solutionName, int order, bool isManaged,
        IEnumerable<LayerComponentDescriptor> components)
    {
        foreach (var component in components)
        {
            var stack = GetOrCreateStack(component.Type, component.Id);
            stack.PushLayer(new ComponentLayer
            {
                SolutionName = solutionName,
                Order = order,
                IsManaged = isManaged,
                Component = component.Component
            });
        }
    }

    /// <summary>
    /// Resolves the effective component for a given stack.
    /// Uses top-wins for non-mergeable types, registered merger for mergeable types.
    /// </summary>
    public MetadataBase? Resolve(LayerStack stack)
    {
        if (stack.RequiresMerge && _mergers.TryGetValue(stack.ComponentType, out var merger))
        {
            return merger.Merge(stack.Layers);
        }
        return stack.ResolveTopWins<MetadataBase>();
    }

    /// <summary>Removes all layers for a solution (uninstall).</summary>
    public void RemoveSolutionLayers(string solutionName)
    {
        foreach (var stack in _stacks.Values)
        {
            stack.RemoveLayer(solutionName);
        }

        var emptyKeys = _stacks.Where(kvp => kvp.Value.Layers.Count == 0)
            .Select(kvp => kvp.Key).ToList();
        foreach (var key in emptyKeys)
            _stacks.Remove(key);
    }
}
