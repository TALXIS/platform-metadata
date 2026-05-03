namespace TALXIS.Platform.Metadata;

/// <summary>
/// Exposes a localized display name.
/// </summary>
public interface IDisplayNamedMetadata
{
    Label DisplayName { get; set; }
}

/// <summary>
/// Exposes a localized description.
/// </summary>
public interface IDescribedMetadata
{
    Label Description { get; set; }
}

/// <summary>
/// Exposes both localized display name and description.
/// </summary>
public interface ILocalizedMetadata : IDisplayNamedMetadata, IDescribedMetadata
{
}

/// <summary>
/// Exposes the version the metadata was introduced in.
/// </summary>
public interface IVersionedMetadata
{
    string? IntroducedVersion { get; set; }
}

/// <summary>
/// Exposes whether the metadata is customizable.
/// </summary>
public interface ICustomizableMetadata
{
    bool IsCustomizable { get; set; }
}

/// <summary>
/// Exposes whether the metadata can be deleted.
/// </summary>
public interface IDeletableMetadata
{
    bool CanBeDeleted { get; set; }
}
