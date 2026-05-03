namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Parsed Power Automate flow definition JSON.
/// </summary>
public sealed class FlowDefinitionMetadata : MetadataBase
{
    private readonly List<FlowConnectionReferenceMetadata> _connectionReferences = new();
    private readonly List<FlowNodeMetadata> _triggers = new();
    private readonly List<FlowNodeMetadata> _actions = new();

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
    /// Gets or sets the original JSON content for round-tripping or deeper inspection.
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
    /// Adds a connection reference.
    /// </summary>
    public void AddConnectionReference(FlowConnectionReferenceMetadata connectionReference) => _connectionReferences.Add(connectionReference);

    /// <summary>
    /// Adds a trigger node.
    /// </summary>
    public void AddTrigger(FlowNodeMetadata trigger) => _triggers.Add(trigger);

    /// <summary>
    /// Adds an action node.
    /// </summary>
    public void AddAction(FlowNodeMetadata action) => _actions.Add(action);
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
    /// Gets or sets the original JSON fragment for the node.
    /// </summary>
    public string? RawJson { get; set; }
}
