namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Ordered stack of solution layers for a single component instance.
/// Resolution: top layer wins for most types. Mergeable types (forms, sitemaps)
/// combine all layers using a registered merge strategy.
/// </summary>
public sealed class LayerStack
{
    private readonly List<ComponentLayer> _layers = new();

    public required ComponentType ComponentType { get; set; }
    public required string ComponentId { get; set; }

    public IReadOnlyList<ComponentLayer> Layers => _layers;

    /// <summary>The topmost (active) layer.</summary>
    public ComponentLayer? ActiveLayer => _layers.Count > 0 ? _layers[_layers.Count - 1] : null;

    /// <summary>The bottom (base) layer.</summary>
    public ComponentLayer? BaseLayer => _layers.Count > 0 ? _layers[0] : null;

    /// <summary>Whether this component requires merge resolution (forms, sitemaps, app modules).</summary>
    public bool RequiresMerge { get; set; }

    public void PushLayer(ComponentLayer layer)
    {
        var index = _layers.FindIndex(l => l.Order > layer.Order);
        if (index < 0)
            _layers.Add(layer);
        else
            _layers.Insert(index, layer);
    }
    public void InsertLayer(int index, ComponentLayer layer) => _layers.Insert(index, layer);

    public bool RemoveLayer(string solutionName)
    {
        var layer = _layers.FirstOrDefault(l => l.SolutionName == solutionName);
        if (layer == null) return false;
        return _layers.Remove(layer);
    }

    /// <summary>
    /// Resolves the effective component for non-mergeable types (top-wins).
    /// Returns the topmost active layer's component.
    /// For mergeable types, use IComponentMerger instead.
    /// </summary>
    public T? ResolveTopWins<T>() where T : MetadataBase
    {
        var active = ActiveLayer;
        if (active?.State != ComponentState.Published) return null;
        return active.Component as T;
    }
}
