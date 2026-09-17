namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="AppModelScaffold"/>: the rendered model-driven app to
/// finalize (sitemap ids) and register.
/// </summary>
public sealed class AppModelScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Schema name of the app (with publisher prefix); registers both the
    /// AppModule (type 80) and SiteMap (type 62) root components.
    /// </summary>
    public string AppSchemaName { get; set; } = "";

    /// <summary>
    /// Path to the rendered AppModuleSiteMap.xml (the managed variant is probed too).
    /// </summary>
    public string SiteMapFilePath { get; set; } = "";
}
