namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Parsed Power Automate flow definition JSON.
/// </summary>
public sealed class FlowDefinitionMetadata : MetadataBase
{
    private readonly List<FlowConnectionReferenceMetadata> _connectionReferences = new();
    private readonly List<FlowNodeMetadata> _triggers = new();
    private readonly List<FlowNodeMetadata> _actions = new();
    private readonly List<FlowDiagnostic> _diagnostics = new();

    public string? Name { get; set; }
    public string? FilePath { get; set; }
    public string? SchemaVersion { get; set; }
    public string? FlowSchema { get; set; }
    public string? ContentVersion { get; set; }

    /// <summary>
    /// The authoritative flow JSON document text used for roundtrip writeback.
    /// Typed flow metadata is a derived projection and must not be treated as a second source of truth.
    /// </summary>
    public string? RawJson { get; set; }

    public IReadOnlyList<FlowConnectionReferenceMetadata> ConnectionReferences => _connectionReferences;
    public IReadOnlyList<FlowNodeMetadata> Triggers => _triggers;
    public IReadOnlyList<FlowNodeMetadata> Actions => _actions;
    public IReadOnlyList<FlowDiagnostic> Diagnostics => _diagnostics;

    public void AddConnectionReference(FlowConnectionReferenceMetadata connectionReference) => _connectionReferences.Add(connectionReference);
    public void AddTrigger(FlowNodeMetadata trigger) => _triggers.Add(trigger);
    public void AddAction(FlowNodeMetadata action) => _actions.Add(action);
    internal void AddDiagnostic(FlowDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public IEnumerable<FlowNodeMetadata> EnumerateNodes()
    {
        foreach (var trigger in _triggers)
            yield return trigger;

        foreach (var action in _actions)
        {
            yield return action;
            foreach (var child in action.EnumerateDescendants())
                yield return child;
        }
    }

    public FlowNodeMetadata? FindNodeByPath(string jsonPath) =>
        EnumerateNodes().FirstOrDefault(n => string.Equals(n.JsonPath, jsonPath, StringComparison.Ordinal));
}

/// <summary>
/// Connection reference declared by a flow definition.
/// </summary>
public sealed class FlowConnectionReferenceMetadata : MetadataBase
{
    public required string Name { get; set; }
    public string? ApiId { get; set; }
    public string? ConnectionName { get; set; }
    public string? ConnectionReferenceLogicalName { get; set; }
}

/// <summary>
/// Trigger or action declared in a flow definition.
/// </summary>
public sealed class FlowNodeMetadata : MetadataBase
{
    private readonly List<FlowNodeMetadata> _children = new();
    private readonly List<FlowRunAfterDependency> _runAfter = new();
    private readonly List<string> _connectionReferenceNames = new();
    private readonly List<FlowExpressionReference> _expressionReferences = new();

    public required string Name { get; set; }
    public required string Kind { get; set; }
    public string? Type { get; set; }
    public string? OperationId { get; set; }
    public string? JsonPath { get; set; }
    public string? ParentPath { get; set; }
    public string? ContainerPath { get; set; }
    public string? BranchName { get; set; }

    /// <summary>Nested actions projected from containers such as Scope, If, Switch, Foreach, and Until.</summary>
    public IReadOnlyList<FlowNodeMetadata> Children => _children;

    /// <summary>Sibling action dependencies declared by this action's runAfter object.</summary>
    public IReadOnlyList<FlowRunAfterDependency> RunAfter => _runAfter;

    /// <summary>Connection reference names used by this node.</summary>
    public IReadOnlyList<string> ConnectionReferenceNames => _connectionReferenceNames;

    /// <summary>Expressions found under this node that reference parameters, variables, actions, items, triggers, or environment values.</summary>
    public IReadOnlyList<FlowExpressionReference> ExpressionReferences => _expressionReferences;

    internal void AddChild(FlowNodeMetadata child) => _children.Add(child);
    internal void AddRunAfter(FlowRunAfterDependency dependency) => _runAfter.Add(dependency);

    internal void AddConnectionReferenceName(string name)
    {
        if (!_connectionReferenceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            _connectionReferenceNames.Add(name);
    }

    internal void AddExpressionReference(FlowExpressionReference reference) => _expressionReferences.Add(reference);

    public IEnumerable<FlowNodeMetadata> EnumerateDescendants()
    {
        foreach (var child in _children)
        {
            yield return child;
            foreach (var descendant in child.EnumerateDescendants())
                yield return descendant;
        }
    }
}

/// <summary>
/// A dependency declared in a Power Automate action runAfter object.
/// </summary>
public sealed class FlowRunAfterDependency
{
    public required string TargetName { get; set; }
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();
    public string? JsonPath { get; set; }
}

/// <summary>
/// A reference discovered inside a flow expression string.
/// </summary>
public sealed class FlowExpressionReference : MetadataBase
{
    public required string Kind { get; set; }
    public string? Name { get; set; }
    public required string Expression { get; set; }
    public string? JsonPath { get; set; }
}

/// <summary>
/// Non-fatal diagnostic produced while projecting a parseable flow JSON document.
/// </summary>
public sealed class FlowDiagnostic
{
    public FlowDiagnosticSeverity Severity { get; set; }
    public required string Code { get; set; }
    public required string Message { get; set; }
    public string? FilePath { get; set; }
    public string? JsonPath { get; set; }
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string? RelatedName { get; set; }
}

/// <summary>
/// Severity for flow projection diagnostics.
/// </summary>
public enum FlowDiagnosticSeverity
{
    Warning,
    Error
}
