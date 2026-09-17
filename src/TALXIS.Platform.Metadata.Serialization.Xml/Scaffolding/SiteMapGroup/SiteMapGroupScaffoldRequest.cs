namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="SiteMapGroupScaffold"/>: the rendered group payload and
/// the area to append it to.
/// </summary>
public sealed class SiteMapGroupScaffoldRequest
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
    /// Path to the rendered group payload (.template.temp/group.xml).
    /// </summary>
    public string GroupFilePath { get; set; } = "";

    /// <summary>
    /// Title of the target area (matched as ResourceId "SitemapDesigner.&lt;title&gt;").
    /// </summary>
    public string AreaTitle { get; set; } = "";
}
