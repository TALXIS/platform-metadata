namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Ordered stack of solution layers for a single component instance.
/// Resolution: top layer wins for most types. Mergeable types (forms, sitemaps)
/// combine all layers using diff/merge.
/// </summary>
public sealed class LayerStack
{
    private readonly List<ComponentLayer> _layers = new();

    public required ComponentType ComponentType { get; set; }
    public required string ComponentId { get; set; }

    public IReadOnlyList<ComponentLayer> Layers => _layers;

    /// <summary>The topmost (active) layer.</summary>
    public ComponentLayer? ActiveLayer => _layers.Count > 0 ? _layers[_layers.Count - 1] : null;

    /// <summary>The bottom (base) layer, typically the publisher's managed solution.</summary>
    public ComponentLayer? BaseLayer => _layers.Count > 0 ? _layers[0] : null;

    public void PushLayer(ComponentLayer layer) => _layers.Add(layer);

    public void InsertLayer(int index, ComponentLayer layer) => _layers.Insert(index, layer);

    public bool RemoveLayer(string solutionName)
    {
        var layer = _layers.FirstOrDefault(l => l.SolutionName == solutionName);
        if (layer == null) return false;
        return _layers.Remove(layer);
    }

    /// <summary>
    /// Resolves the effective content for this component.
    /// For non-mergeable types: returns the top layer's content (top-wins).
    /// For mergeable types: returns null (caller must use merge engine).
    /// </summary>
    public string? ResolveTopWins()
    {
        var active = ActiveLayer;
        return active?.State == ComponentState.Published ? active.XmlContent : null;
    }

    /// <summary>Whether this component requires merge resolution (forms, sitemaps).</summary>
    public bool RequiresMerge { get; set; }
}
