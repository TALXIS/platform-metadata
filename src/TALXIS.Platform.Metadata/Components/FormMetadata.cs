using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Components;

public sealed class FormMetadata : MetadataBase
{
    public required string FormId { get; set; }
    public string? FormType { get; set; }
    public string? Name { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();
    public string? IntroducedVersion { get; set; }
    public int? FormPresentation { get; set; }
    public int? FormActivationState { get; set; }
    public bool IsCustomizable { get; set; }
    public bool CanBeDeleted { get; set; }
    public string? EntityLogicalName { get; set; }

    /// <summary>The form body as a mergeable tree (tabs/sections/rows/cells/controls).</summary>
    public MergeableNode? Body { get; set; }
}
