namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="SiteMapAreaScaffold"/>: the rendered area payload and
/// the app sitemap to append it to.
/// </summary>
public sealed class SiteMapAreaScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the app's AppModuleSiteMap.xml (the managed variant is probed too).
    /// </summary>
    public string SiteMapFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered area payload (.template.temp/area.xml).
    /// </summary>
    public string AreaFilePath { get; set; } = "";
}
