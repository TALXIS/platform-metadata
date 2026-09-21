using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Fills type code, name and id tokens in the rendered data.xml, renames it
/// after the source file, copies the source into WebResources/, and registers
/// the web resource in Solution.xml (type 61, by schema name).
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

        // Fills the capital-id token before the plain id token because the former contains the latter.
        var content = File.ReadAllText(request.DataXmlFilePath);
        content = content
            .Replace("wrtypeexample", typeCode.ToString())
            .Replace("wridexamplecapital", id.ToUpperInvariant())
            .Replace("wridexample", id)
            .Replace("fileexampledisplayname", fileName)
            .Replace("fileexamplename", fileName);

        var webResourcesDir = Path.GetDirectoryName(request.DataXmlFilePath)!;
        var dataXmlTarget = Path.Combine(webResourcesDir, $"{schemaName}.data.xml");
        // Appends two newlines to the file content.
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
