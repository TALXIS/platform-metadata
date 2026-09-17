namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="WebResourceScaffold"/>: the source file to publish as
/// a web resource and the rendered data.xml stub to finalize.
/// </summary>
public sealed class WebResourceScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Path to the user's source file (the WebResourceItemPath parameter).
    /// </summary>
    public string SourceFilePath { get; set; } = "";

    /// <summary>
    /// Path to the rendered web resource data.xml stub.
    /// </summary>
    public string DataXmlFilePath { get; set; } = "";

    /// <summary>
    /// Publisher prefix used in the web resource schema name.
    /// </summary>
    public string PublisherPrefix { get; set; } = "";

    /// <summary>
    /// GUID for the web resource, without braces; null generates a new one.
    /// </summary>
    public string? WebResourceId { get; set; }
}
