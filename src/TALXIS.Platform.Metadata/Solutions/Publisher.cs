namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Dataverse publisher metadata referenced by a solution.
/// </summary>
public sealed class Publisher : MetadataBase, ILocalizedMetadata
{
    /// <summary>
    /// Gets or sets the unique publisher name.
    /// </summary>
    public required string UniqueName { get; set; }

    /// <summary>
    /// Gets or sets the customization prefix used for schema names.
    /// </summary>
    public required string Prefix { get; set; }

    /// <inheritdoc />
    public Label DisplayName { get; set; } = new();

    /// <inheritdoc />
    public Label Description { get; set; } = new();

    /// <summary>
    /// Gets or sets the numeric prefix used for option-set values.
    /// </summary>
    public int? OptionValuePrefix { get; set; }
}
