namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="SiteMapSubAreaScaffold"/>: the sub area to build and
/// the group of the app sitemap to append it to.
/// </summary>
public sealed class SiteMapSubAreaScaffoldRequest
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
    /// Title of the target area (matched as ResourceId "SitemapDesigner.&lt;title&gt;").
    /// </summary>
    public string AreaTitle { get; set; } = "";

    /// <summary>
    /// Title of the target group (matched as ResourceId "SitemapDesigner.&lt;title&gt;").
    /// </summary>
    public string GroupTitle { get; set; } = "";

    /// <summary>
    /// Page kind: entity, entitylist, dashboard, control, custom, webresource or genpage.
    /// </summary>
    public string PageType { get; set; } = "";

    /// <summary>
    /// Display title of the sub area.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Logical name of the entity (entity/entitylist; also emitted for control).
    /// </summary>
    public string? EntityLogicalName { get; set; }

    /// <summary>
    /// View id for the entitylist page type.
    /// </summary>
    public string? ViewId { get; set; }

    /// <summary>
    /// Dashboard id for the dashboard page type.
    /// </summary>
    public string? DashboardId { get; set; }

    /// <summary>
    /// Control name for the control page type.
    /// </summary>
    public string? ControlName { get; set; }

    /// <summary>
    /// Generated page id for the genpage page type.
    /// </summary>
    public string? GenPageId { get; set; }

    /// <summary>
    /// Custom page name for the custom page type.
    /// </summary>
    public string? CustomPageName { get; set; }

    /// <summary>
    /// Web resource name for the webresource page type.
    /// </summary>
    public string? WebResourceName { get; set; }
}
