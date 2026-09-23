using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Components;

public sealed class FormMetadata : MetadataBase, ILocalizedMetadata, IVersionedMetadata, ICustomizableMetadata, IDeletableMetadata, ISolutionComponent
{
    public required string FormId { get; set; }

    /// <inheritdoc />
    public ComponentIdentity Identity => new(ComponentType.SystemForm, FormId);

    /// <inheritdoc />
    public string DocumentKey => $"Form:{EntityLogicalName}:{FormId}";
    public string? FormType { get; set; }
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
