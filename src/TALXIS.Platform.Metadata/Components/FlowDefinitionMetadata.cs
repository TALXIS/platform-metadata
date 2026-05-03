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

    /// <summary>
    /// Gets or sets the flow display name, when available.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the relative path of the source JSON file.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the schema-version marker from the outer JSON payload.
    /// </summary>
    public string? SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the inner flow-schema URI.
    /// </summary>
    public string? FlowSchema { get; set; }

    /// <summary>
    /// Gets or sets the flow content version.
    /// </summary>
    public string? ContentVersion { get; set; }

    /// <summary>
    /// Gets or sets the authoritative flow JSON document text used for roundtrip writeback.
    /// Typed flow metadata is a derived projection and must not be treated as a second source of truth.
    /// </summary>
    public string? RawJson { get; set; }

    /// <summary>
    /// Gets the connection references used by the flow.
    /// </summary>
    public IReadOnlyList<FlowConnectionReferenceMetadata> ConnectionReferences => _connectionReferences;

    /// <summary>
    /// Gets the parsed trigger nodes.
    /// </summary>
    public IReadOnlyList<FlowNodeMetadata> Triggers => _triggers;

    /// <summary>
    /// Gets the parsed action nodes.
    /// </summary>
    public IReadOnlyList<FlowNodeMetadata> Actions => _actions;

    /// <summary>
    /// Gets non-fatal diagnostics produced while projecting the flow JSON.
    /// </summary>
    public IReadOnlyList<FlowDiagnostic> Diagnostics => _diagnostics;

    internal void AddConnectionReference(FlowConnectionReferenceMetadata connectionReference) => _connectionReferences.Add(connectionReference);
    internal void AddTrigger(FlowNodeMetadata trigger) => _triggers.Add(trigger);
    internal void AddAction(FlowNodeMetadata action) => _actions.Add(action);
    internal void AddDiagnostic(FlowDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    /// <summary>
    /// Enumerates triggers, top-level actions, and nested action descendants.
    /// </summary>
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

    /// <summary>
    /// Finds the first node with the supplied JSON path.
    /// </summary>
    /// <param name="jsonPath">JSON path to find.</param>
    /// <returns>The matching node, or <see langword="null"/> when none is found.</returns>
    public FlowNodeMetadata? FindNodeByPath(string jsonPath) =>
        EnumerateNodes().FirstOrDefault(n => string.Equals(n.JsonPath, jsonPath, StringComparison.Ordinal));
}

/// <summary>
/// Connection reference declared by a flow definition.
/// </summary>
public sealed class FlowConnectionReferenceMetadata : MetadataBase
{
    /// <summary>
    /// Gets or sets the connection-reference name in the flow JSON.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the connector API identifier.
    /// </summary>
    public string? ApiId { get; set; }

    /// <summary>
    /// Gets or sets the referenced connection name.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the Dataverse logical name of the connection reference.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the node name in the flow definition.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the node is a trigger or action kind.
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// Gets or sets the connector/node type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the operation identifier.
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Gets or sets the JSON path of this node.
    /// </summary>
    public string? JsonPath { get; set; }

    /// <summary>
    /// Gets or sets the JSON path of the parent node.
    /// </summary>
    public string? ParentPath { get; set; }

    /// <summary>
    /// Gets or sets the JSON path of the container that owns this node.
    /// </summary>
    public string? ContainerPath { get; set; }

    /// <summary>
    /// Gets or sets the branch name when the node belongs to a branching container.
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Gets nested actions projected from containers such as Scope, If, Switch, Foreach, and Until.
    /// </summary>
    public IReadOnlyList<FlowNodeMetadata> Children => _children;

    /// <summary>
    /// Gets sibling action dependencies declared by this action's runAfter object.
    /// </summary>
    public IReadOnlyList<FlowRunAfterDependency> RunAfter => _runAfter;

    /// <summary>
    /// Gets connection reference names used by this node.
    /// </summary>
    public IReadOnlyList<string> ConnectionReferenceNames => _connectionReferenceNames;

    /// <summary>
    /// Gets expressions found under this node that reference parameters, variables, actions, items, triggers, or environment values.
    /// </summary>
    public IReadOnlyList<FlowExpressionReference> ExpressionReferences => _expressionReferences;

    internal void AddChild(FlowNodeMetadata child) => _children.Add(child);
    internal void AddRunAfter(FlowRunAfterDependency dependency) => _runAfter.Add(dependency);

    internal void AddConnectionReferenceName(string name)
    {
        if (!_connectionReferenceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            _connectionReferenceNames.Add(name);
    }

    internal void AddExpressionReference(FlowExpressionReference reference) => _expressionReferences.Add(reference);

    /// <summary>
    /// Enumerates all nested descendant nodes.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the target action name.
    /// </summary>
    public required string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the statuses that satisfy the dependency.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the JSON path of the dependency declaration.
    /// </summary>
    public string? JsonPath { get; set; }
}

/// <summary>
/// A reference discovered inside a flow expression string.
/// </summary>
public sealed class FlowExpressionReference : MetadataBase
{
    /// <summary>
    /// Gets or sets the expression reference kind.
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// Gets or sets the referenced name, when available.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the expression text.
    /// </summary>
    public required string Expression { get; set; }

    /// <summary>
    /// Gets or sets the JSON path of the expression.
    /// </summary>
    public string? JsonPath { get; set; }
}

/// <summary>
/// Non-fatal diagnostic produced while projecting a parseable flow JSON document.
/// </summary>
public sealed class FlowDiagnostic
{
    /// <summary>
    /// Gets or sets the diagnostic severity.
    /// </summary>
    public FlowDiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the diagnostic code.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Gets or sets the diagnostic message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the JSON path associated with the diagnostic.
    /// </summary>
    public string? JsonPath { get; set; }

    /// <summary>
    /// Gets or sets the 1-based line number, when available.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Gets or sets the 1-based column number, when available.
    /// </summary>
    public int? Column { get; set; }

    /// <summary>
    /// Gets or sets an optional related component or node name.
    /// </summary>
    public string? RelatedName { get; set; }
}

/// <summary>
/// Severity for flow projection diagnostics.
/// </summary>
public enum FlowDiagnosticSeverity
{
    /// <summary>
    /// Warning diagnostic.
    /// </summary>
    Warning,

    /// <summary>
    /// Error diagnostic.
    /// </summary>
    Error
}
