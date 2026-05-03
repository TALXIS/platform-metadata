namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Manages component layers across a workspace.
/// Maps component identity (type + id) to its layer stack.
/// </summary>
public sealed class SolutionLayerManager
{
    private readonly Dictionary<string, LayerStack> _stacks = new();

    /// <summary>Gets or creates a layer stack for a component.</summary>
    public LayerStack GetOrCreateStack(ComponentType componentType, string componentId)
    {
        var key = $"{(int)componentType}:{componentId}";
        if (!_stacks.TryGetValue(key, out var stack))
        {
            stack = new LayerStack
            {
                ComponentType = componentType,
                ComponentId = componentId
            };
            _stacks[key] = stack;
        }
        return stack;
    }

    public LayerStack? FindStack(ComponentType componentType, string componentId)
    {
        var key = $"{(int)componentType}:{componentId}";
        return _stacks.TryGetValue(key, out var stack) ? stack : null;
    }

    public IReadOnlyCollection<LayerStack> AllStacks => _stacks.Values;

    /// <summary>
    /// Imports a solution's components as a new layer.
    /// Each component gets a layer with the given solution name and order.
    /// </summary>
    public void ImportSolutionLayer(string solutionName, int order, bool isManaged,
        IEnumerable<(ComponentType type, string id, string? xmlContent)> components)
    {
        foreach (var (type, id, xml) in components)
        {
            var stack = GetOrCreateStack(type, id);
            stack.PushLayer(new ComponentLayer
            {
                SolutionName = solutionName,
                Order = order,
                IsManaged = isManaged,
                XmlContent = xml
            });
        }
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
