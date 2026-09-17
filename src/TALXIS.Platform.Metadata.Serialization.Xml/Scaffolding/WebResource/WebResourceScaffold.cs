using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-webresource template post-action scripts:
/// stamps type code, names and ids into the rendered data.xml (token replacement,
/// preserved from the old scripts), renames it after the source file, copies the
/// source into WebResources/ and registers the web resource in Solution.xml
/// (type 61, by schema name). The Customizations.xml WebResources-node patch is
/// deliberately dropped; the .js build-target hookup stays in the template script
/// (a dotnet CLI call, not an XML mutation).
/// </summary>
public static class WebResourceScaffold
{
    public static ScaffoldResult Apply(WebResourceScaffoldRequest request)
    {
        if (!File.Exists(request.SourceFilePath))
            throw new FileNotFoundException($"Web resource source file not found: {request.SourceFilePath}");
        if (!File.Exists(request.DataXmlFilePath))
            throw new FileNotFoundException($"Web resource data.xml stub not found: {request.DataXmlFilePath}");

        var fileName = Path.GetFileName(request.SourceFilePath);
        var schemaName = $"{request.PublisherPrefix}_{fileName}";
        var id = request.WebResourceId ?? Guid.NewGuid().ToString();
        var typeCode = TypeCodeFor(Path.GetExtension(request.SourceFilePath).ToLowerInvariant());

        // Same token replacements as the old SetWebresourceType/SetupWRDataFile pair;
        // the capital-id token must go before the plain one (it contains it).
        var content = File.ReadAllText(request.DataXmlFilePath);
        content = content
            .Replace("wrtypeexample", typeCode.ToString())
            .Replace("wridexamplecapital", id.ToUpperInvariant())
            .Replace("wridexample", id)
            .Replace("fileexampledisplayname", fileName)
            .Replace("fileexamplename", fileName);

        var webResourcesDir = Path.GetDirectoryName(request.DataXmlFilePath)!;
        var dataXmlTarget = Path.Combine(webResourcesDir, $"{schemaName}.data.xml");
        // The old pipeline piped the content through Set-Content twice, adding a newline each time.
        File.WriteAllText(dataXmlTarget, content + "\r\n" + "\r\n");
        if (!string.Equals(Path.GetFullPath(dataXmlTarget), Path.GetFullPath(request.DataXmlFilePath), StringComparison.OrdinalIgnoreCase))
            File.Delete(request.DataXmlFilePath);

        File.Copy(request.SourceFilePath, Path.Combine(webResourcesDir, schemaName), overwrite: true);

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.WebResource,
            SchemaName = schemaName,
            Behavior = 0,
        });
        return new ScaffoldResult();
    }

    private static int TypeCodeFor(string extension) => extension switch
    {
        ".htm" or ".html" => 1,
        ".css" => 2,
        ".js" => 3,
        ".xml" => 4,
        ".png" => 5,
        ".jpg" => 6,
        ".gif" => 7,
        ".xap" => 8,
        ".xsl" or ".xslt" => 9,
        ".ico" => 10,
        ".svg" => 11,
        ".resx" => 12,
        _ => 0,
    };
}
