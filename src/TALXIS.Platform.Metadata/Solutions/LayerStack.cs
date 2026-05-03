namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Ordered stack of solution layers for a single component instance.
/// Resolution: top layer wins for most types. Mergeable types (forms, sitemaps)
/// combine all layers using a registered merge strategy.
/// </summary>
public sealed class LayerStack
{
    private readonly List<ComponentLayer> _layers = new();

    /// <summary>
    /// Gets or sets the component type represented by the stack.
    /// </summary>
    public required ComponentType ComponentType { get; set; }

    /// <summary>
    /// Gets or sets the component identifier within the type.
    /// </summary>
    public required string ComponentId { get; set; }

    /// <summary>
    /// Gets the ordered layers from base to topmost.
    /// </summary>
    public IReadOnlyList<ComponentLayer> Layers => _layers;

    /// <summary>The topmost (active) layer.</summary>
    public ComponentLayer? ActiveLayer => _layers.Count > 0 ? _layers[_layers.Count - 1] : null;

    /// <summary>The bottom (base) layer.</summary>
    public ComponentLayer? BaseLayer => _layers.Count > 0 ? _layers[0] : null;

    /// <summary>Whether this component requires merge resolution (forms, sitemaps, app modules).</summary>
    public bool RequiresMerge { get; set; }

    /// <summary>
    /// Adds a layer while keeping the stack ordered by <see cref="ComponentLayer.Order"/>.
    /// </summary>
    public void PushLayer(ComponentLayer layer)
    {
        var index = _layers.FindIndex(existing => CompareLayerOrder(existing, layer) > 0);
        if (index < 0)
            _layers.Add(layer);
        else
            _layers.Insert(index, layer);
    }

    /// <summary>
    /// Inserts a layer at a specific position.
    /// </summary>
    public void InsertLayer(int index, ComponentLayer layer) => _layers.Insert(index, layer);

    /// <summary>
    /// Removes all layers belonging to the supplied layer or source solution.
    /// </summary>
    public bool RemoveLayer(string solutionName)
    {
        var removed = _layers.RemoveAll(l =>
            string.Equals(l.SolutionUniqueName, solutionName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(l.SourceSolutionUniqueName, solutionName, StringComparison.OrdinalIgnoreCase));
        return removed > 0;
    }

    /// <summary>
    /// Resolves the effective component for non-mergeable types (top-wins).
    /// Returns the topmost active layer's component.
    /// For mergeable types, use IComponentMerger instead.
    /// </summary>
    public T? ResolveTopWins<T>() where T : MetadataBase
    {
        var active = ActiveLayer;
        if (active?.State != ComponentState.Publish) return null;
        return active.Component as T;
    }

    private static int CompareLayerOrder(ComponentLayer left, ComponentLayer right)
    {
        var layerKindCompare = GetLayerRank(left.LayerKind).CompareTo(GetLayerRank(right.LayerKind));
        if (layerKindCompare != 0)
            return layerKindCompare;

        var orderCompare = left.Order.CompareTo(right.Order);
        if (orderCompare != 0)
            return orderCompare;

        return left.SourceOrder.CompareTo(right.SourceOrder);
    }

    private static int GetLayerRank(SolutionLayerKind kind) =>
        kind switch
        {
            SolutionLayerKind.System => 0,
            SolutionLayerKind.Default => 1,
            SolutionLayerKind.Managed => 2,
            SolutionLayerKind.Active => 3,
            _ => 2
        };
}
