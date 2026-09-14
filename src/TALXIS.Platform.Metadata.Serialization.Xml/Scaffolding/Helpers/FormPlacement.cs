namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Where inside a form a scaffolded element lands: tab, column, section and row
/// targeting with id/index fallbacks, or the tab footer.
/// </summary>
public sealed class FormPlacement
{
    /// <summary>
    /// GUID of the target tab, without braces.
    /// </summary>
    public string? TabId { get; set; }

    /// <summary>
    /// 1-based index of the target tab.
    /// </summary>
    public string? TabIndex { get; set; }

    /// <summary>
    /// 1-based index of the target column within the tab.
    /// </summary>
    public string? ColumnIndex { get; set; }

    /// <summary>
    /// GUID of the target section, without braces.
    /// </summary>
    public string? SectionId { get; set; }

    /// <summary>
    /// 1-based index of the target section within the column.
    /// </summary>
    public string? SectionIndex { get; set; }

    /// <summary>
    /// 1-based index of the target row within the section.
    /// </summary>
    public string? RowIndex { get; set; }

    /// <summary>
    /// Target the tab footer instead of a column/section.
    /// </summary>
    public bool SetToTabFooter { get; set; }
}
