namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="RibbonButtonHideScaffold"/>: the entity ribbon to
/// patch and the rendered hide actions to append.
/// </summary>
public sealed class RibbonButtonHideScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the entity's RibbonDiff.xml; it must already exist.
    /// </summary>
    public string RibbonDiffFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered payload with the HideCustomAction entries.
    /// </summary>
    public string HideFilePath { get; set; } = "";
}
