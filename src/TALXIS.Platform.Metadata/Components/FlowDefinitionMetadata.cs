namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Parsed Power Automate flow definition JSON.
/// </summary>
public sealed class FlowDefinitionMetadata : MetadataBase
{
    private readonly List<FlowConnectionReferenceMetadata> _connectionReferences = new();
    private readonly List<FlowNodeMetadata> _triggers = new();
    private readonly List<FlowNodeMetadata> _actions = new();

    public string? Name { get; set; }
    public string? FilePath { get; set; }
    public string? SchemaVersion { get; set; }
    public string? FlowSchema { get; set; }
    public string? ContentVersion { get; set; }
    public string? RawJson { get; set; }

    public IReadOnlyList<FlowConnectionReferenceMetadata> ConnectionReferences => _connectionReferences;
    public IReadOnlyList<FlowNodeMetadata> Triggers => _triggers;
    public IReadOnlyList<FlowNodeMetadata> Actions => _actions;

    public void AddConnectionReference(FlowConnectionReferenceMetadata connectionReference) => _connectionReferences.Add(connectionReference);
    public void AddTrigger(FlowNodeMetadata trigger) => _triggers.Add(trigger);
    public void AddAction(FlowNodeMetadata action) => _actions.Add(action);
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
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public string? Type { get; set; }
    public string? OperationId { get; set; }
    public string? RawJson { get; set; }
}
