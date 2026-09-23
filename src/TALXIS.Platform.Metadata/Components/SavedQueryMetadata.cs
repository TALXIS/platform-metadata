using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Components;

public sealed class SavedQueryMetadata : MetadataBase, ILocalizedMetadata, IVersionedMetadata, ICustomizableMetadata, IDeletableMetadata, ISolutionComponent
{
    public required string SavedQueryId { get; set; }

    /// <inheritdoc />
    public ComponentIdentity Identity => new(ComponentType.SavedQuery, SavedQueryId);

    /// <inheritdoc />
    public string DocumentKey => $"View:{EntityLogicalName}:{SavedQueryId}";
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();
    public string? EntityLogicalName { get; set; }
    public int? QueryType { get; set; }
    public bool IsDefault { get; set; }
    public bool IsCustomizable { get; set; }
    public bool CanBeDeleted { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsQuickFindQuery { get; set; }
    public string? IntroducedVersion { get; set; }
    public string? FetchXml { get; set; }
    public string? LayoutXml { get; set; }
}
