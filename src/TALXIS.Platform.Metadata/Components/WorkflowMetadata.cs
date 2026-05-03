namespace TALXIS.Platform.Metadata.Components;

public sealed class WorkflowMetadata : MetadataBase
{
    public required string WorkflowId { get; set; }
    public Label Name { get; set; } = new();
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
}
