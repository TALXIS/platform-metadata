using System.Reflection;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Loads XSD schemas from embedded resources.
/// </summary>
public static class SchemaResourceLoader
{
    private static readonly Assembly _assembly = typeof(SchemaResourceLoader).Assembly;
    private static readonly string _prefix = typeof(SchemaResourceLoader).Namespace + ".Schemas.";

    /// <summary>
    /// Opens a stream for an embedded XSD schema by filename (e.g., "Entity.xsd").
    /// </summary>
    public static Stream OpenSchema(string fileName)
    {
        var resourceName = _prefix + fileName;
        return _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded schema '{fileName}' not found. Available: {string.Join(", ", GetAvailableSchemas())}");
    }

    /// <summary>
    /// Lists all available embedded schema filenames.
    /// </summary>
    public static IEnumerable<string> GetAvailableSchemas()
    {
        return _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(_prefix))
            .Select(n => n.Substring(_prefix.Length));
    }
}
