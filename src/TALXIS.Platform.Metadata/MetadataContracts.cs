namespace TALXIS.Platform.Metadata;

/// <summary>
/// Exposes a localized display name.
/// </summary>
public interface IDisplayNamedMetadata
{
    /// <summary>
    /// Gets or sets the localized display name shown to users.
    /// </summary>
    Label DisplayName { get; set; }
}

/// <summary>
/// Exposes a localized description.
/// </summary>
public interface IDescribedMetadata
{
    /// <summary>
    /// Gets or sets the localized description shown to users.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the product version that first introduced the metadata item.
    /// </summary>
    string? IntroducedVersion { get; set; }
}

/// <summary>
/// Exposes whether the metadata is customizable.
/// </summary>
public interface ICustomizableMetadata
{
    /// <summary>
    /// Gets or sets whether the metadata item is customizable in the target environment.
    /// </summary>
    bool IsCustomizable { get; set; }
}

/// <summary>
/// Exposes whether the metadata can be deleted.
/// </summary>
public interface IDeletableMetadata
{
    /// <summary>
    /// Gets or sets whether the metadata item can be deleted from a solution.
    /// </summary>
    bool CanBeDeleted { get; set; }
}
