namespace TALXIS.Platform.Metadata.Components;

public sealed class WorkflowMetadata : MetadataBase, ILocalizedMetadata, IVersionedMetadata, ICustomizableMetadata
{
    public required string WorkflowId { get; set; }
    public Label DisplayName { get; set; } = new();
    public string? UniqueName { get; set; }
    public Label Description { get; set; } = new();
    public int? Category { get; set; }
    public int? Type { get; set; }
    public int? Mode { get; set; }
    public int? Scope { get; set; }
    public string? PrimaryEntity { get; set; }
    public string? XamlFileName { get; set; }
    public int? StateCode { get; set; }
    public int? StatusCode { get; set; }
    public bool TriggerOnCreate { get; set; }
    public bool TriggerOnDelete { get; set; }
    public bool OnDemand { get; set; }
    public string? IntroducedVersion { get; set; }
    public bool IsCustomizable { get; set; }
    public int? ModernFlowType { get; set; }
    public string? JsonFileName { get; set; }
    public int? CreateStage { get; set; }
    public int? UpdateStage { get; set; }
    public int? DeleteStage { get; set; }
    public int? Rank { get; set; }
    public int? ProcessOrder { get; set; }
}
