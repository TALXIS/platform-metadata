namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Columns no create or update can set, according to their API flags.
/// </summary>
public static class ReadOnlyColumns
{
    /// <summary>
    /// Whether <paramref name="attribute"/> is valid neither for create nor for update. File and image columns are not, since their content
    /// is uploaded separately; a flag the metadata does not carry (<c>null</c>) counts as allowed.
    /// </summary>
    public static bool Contains(AttributeMetadata attribute) =>
        attribute.IsValidForCreate == false && attribute.IsValidForUpdate == false
        && attribute.AttributeType is not (AttributeType.File or AttributeType.Image);
}
