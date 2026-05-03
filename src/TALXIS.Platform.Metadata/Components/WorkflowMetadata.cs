namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Workflow, action, business-rule, or modern-flow metadata loaded from workflow XML.
/// </summary>
public sealed class WorkflowMetadata : MetadataBase, ILocalizedMetadata, IVersionedMetadata, ICustomizableMetadata
{
    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <inheritdoc />
    public Label DisplayName { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique process name.
    /// </summary>
    public string? UniqueName { get; set; }

    /// <inheritdoc />
    public Label Description { get; set; } = new();

    /// <summary>
    /// Gets or sets the Dataverse process category.
    /// </summary>
    public int? Category { get; set; }

    /// <summary>
    /// Gets or sets the workflow type code.
    /// </summary>
    public int? Type { get; set; }

    /// <summary>
    /// Gets or sets the execution mode.
    /// </summary>
    public int? Mode { get; set; }

    /// <summary>
    /// Gets or sets the process scope.
    /// </summary>
    public int? Scope { get; set; }

    /// <summary>
    /// Gets or sets the primary table logical name.
    /// </summary>
    public string? PrimaryEntity { get; set; }

    /// <summary>
    /// Gets or sets the XAML file name for classic workflow implementations.
    /// </summary>
    public string? XamlFileName { get; set; }

    /// <summary>
    /// Gets or sets the state code.
    /// </summary>
    public int? StateCode { get; set; }

    /// <summary>
    /// Gets or sets the status code.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets whether the process triggers on create.
    /// </summary>
    public bool TriggerOnCreate { get; set; }

    /// <summary>
    /// Gets or sets whether the process triggers on delete.
    /// </summary>
    public bool TriggerOnDelete { get; set; }

    /// <summary>
    /// Gets or sets whether the process can be invoked on demand.
    /// </summary>
    public bool OnDemand { get; set; }

    /// <inheritdoc />
    public string? IntroducedVersion { get; set; }

    /// <inheritdoc />
    public bool IsCustomizable { get; set; }

    /// <summary>
    /// Gets or sets the modern-flow type discriminator when the workflow represents a cloud flow.
    /// </summary>
    public int? ModernFlowType { get; set; }

    /// <summary>
    /// Gets or sets the sibling JSON file name that contains the flow definition.
    /// </summary>
    public string? JsonFileName { get; set; }

    /// <summary>
    /// Gets or sets the create pipeline stage for workflow triggers.
    /// </summary>
    public int? CreateStage { get; set; }

    /// <summary>
    /// Gets or sets the update pipeline stage for workflow triggers.
    /// </summary>
    public int? UpdateStage { get; set; }

    /// <summary>
    /// Gets or sets the delete pipeline stage for workflow triggers.
    /// </summary>
    public int? DeleteStage { get; set; }

    /// <summary>
    /// Gets or sets the workflow rank.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// Gets or sets the process execution order.
    /// </summary>
    public int? ProcessOrder { get; set; }

    /// <summary>
    /// Gets or sets the parsed flow-definition JSON when this workflow represents a modern cloud flow.
    /// </summary>
    public FlowDefinitionMetadata? FlowDefinition { get; set; }
}
