namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="RibbonButtonScaffold"/>: the entity ribbon to patch
/// and the rendered button payloads to merge into it.
/// </summary>
public sealed class RibbonButtonScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the entity's RibbonDiff.xml; created from the empty payload when missing.
    /// </summary>
    public string RibbonDiffFilePath { get; set; } = "";

    /// <summary>
    /// Path to the empty RibbonDiff payload used when the entity has no ribbon yet.
    /// </summary>
    public string EmptyRibbonFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered command definition payload.
    /// </summary>
    public string CommandDefinitionFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered loc labels payload.
    /// </summary>
    public string LocLabelsFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered custom action payload.
    /// </summary>
    public string CustomActionFilePath { get; set; } = "";

    /// <summary>
    /// Display label of the button; the logical name derives from it.
    /// </summary>
    public string ButtonLabel { get; set; } = "";

    /// <summary>
    /// 16x16 icon web resource name; null or the default sentinel means none.
    /// </summary>
    public string? Image16by16 { get; set; }

    /// <summary>
    /// 32x32 icon web resource name; null or the default sentinel means none.
    /// </summary>
    public string? Image32by32 { get; set; }

    /// <summary>
    /// Modern image web resource name; null or the default sentinel means none.
    /// </summary>
    public string? ModernImage { get; set; }
}
