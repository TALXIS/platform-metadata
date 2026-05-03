namespace TALXIS.Platform.Metadata.Solutions;

using TALXIS.Platform.Metadata.Merging;

/// <summary>
/// Manages component layers across a workspace.
/// Maps component identity (type + id) to its layer stack.
/// </summary>
public sealed class SolutionLayerManager
{
    /// <summary>
    /// Dataverse name of the shared unmanaged active layer.
    /// </summary>
    public const string ActiveSolutionName = "Active";

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
    public LayerStack GetOrCreateStack(ComponentType componentType, string componentObjectId)
    {
        var key = $"{(int)componentType}:{componentObjectId}";
        if (!_stacks.TryGetValue(key, out var stack))
        {
            stack = new LayerStack
            {
                ComponentType = componentType,
                ComponentObjectId = componentObjectId,
                RequiresMerge = ComponentDefinitionRegistry.GetByType(componentType)?.IsMergeable == true
            };
            _stacks[key] = stack;
        }
        return stack;
    }

    /// <summary>
    /// Finds an existing layer stack without creating a new one.
    /// </summary>
    public LayerStack? FindStack(ComponentType componentType, string componentObjectId)
    {
        var key = $"{(int)componentType}:{componentObjectId}";
        return _stacks.TryGetValue(key, out var stack) ? stack : null;
    }

    /// <summary>
    /// Gets all tracked component stacks.
    /// </summary>
    public IReadOnlyCollection<LayerStack> Stacks => _stacks.Values;

    /// <summary>
    /// Registers or replaces the merger used for a mergeable component type.
    /// </summary>
    public void RegisterMerger(IComponentMerger merger)
    {
        if (merger == null) throw new ArgumentNullException(nameof(merger));

        _mergers[merger.ComponentType] = merger;
    }

    /// <summary>
    /// Imports managed solution components as a Dataverse-style managed layer.
    /// </summary>
    /// <param name="solution">Managed solution that owns the layer.</param>
    /// <param name="importOrder">Caller-defined import order.</param>
    /// <param name="components">Components in this layer.</param>
    /// <param name="sourceRootPath">Optional source project root for diagnostics and write-back.</param>
    public void ImportManagedLayer(
        Solution solution,
        int importOrder,
        IEnumerable<LayerComponentDescriptor> components,
        string? sourceRootPath = null)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));
        if (components == null) throw new ArgumentNullException(nameof(components));

        ImportLayer(
            solution.UniqueName,
            solution.UniqueName,
            SolutionLayerKind.Managed,
            importOrder,
            true,
            components,
            sourceRootPath);
    }

    /// <summary>
    /// Imports unmanaged solution components as source-owned snapshots of the shared Active layer.
    /// </summary>
    /// <param name="solution">Unmanaged solution project that owns the source snapshots.</param>
    /// <param name="importOrder">Caller-defined source precedence order.</param>
    /// <param name="components">Components in this source snapshot.</param>
    /// <param name="sourceRootPath">Optional source project root for diagnostics and write-back.</param>
    public void ImportActiveLayerSnapshot(
        Solution solution,
        int importOrder,
        IEnumerable<LayerComponentDescriptor> components,
        string? sourceRootPath = null)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));
        if (components == null) throw new ArgumentNullException(nameof(components));

        ImportLayer(
            ActiveSolutionName,
            solution.UniqueName,
            SolutionLayerKind.Active,
            importOrder,
            false,
            components,
            sourceRootPath);
    }

    private void ImportLayer(
        string layerSolutionName,
        string sourceSolutionName,
        SolutionLayerKind layerKind,
        int order,
        bool isManaged,
        IEnumerable<LayerComponentDescriptor> components,
        string? sourceRootPath)
    {
        foreach (var component in components)
        {
            var stack = GetOrCreateStack(component.Type, component.ObjectId);
            stack.AddLayer(new ComponentLayer
            {
                LayerSolutionUniqueName = layerSolutionName,
                SourceSolutionUniqueName = sourceSolutionName,
                LayerKind = layerKind,
                SourceRootPath = sourceRootPath,
                SourceDocumentKey = component.SourceDocumentKey,
                SourceOrder = order,
                LayerOrder = order,
                IsManaged = isManaged,
                Metadata = component.Metadata
            });
        }
    }

    /// <summary>
    /// Resolves the effective component for a given stack.
    /// Uses top-wins for non-mergeable types, registered merger for mergeable types.
    /// </summary>
    public MetadataBase? Resolve(LayerStack stack)
    {
        if (stack == null) throw new ArgumentNullException(nameof(stack));

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
            stack.RemoveLayersForSolution(solutionName);
        }

        var emptyKeys = _stacks.Where(kvp => kvp.Value.Layers.Count == 0)
            .Select(kvp => kvp.Key).ToList();
        foreach (var key in emptyKeys)
            _stacks.Remove(key);
    }
}
