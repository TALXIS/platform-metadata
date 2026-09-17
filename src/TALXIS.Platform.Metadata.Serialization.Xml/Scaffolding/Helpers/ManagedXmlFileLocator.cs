namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Resolves a component file or its managed variant: for ".../AppModuleSiteMap.xml",
/// returns that path or its "_managed" sibling, or null when neither exists.
/// </summary>
internal static class ManagedXmlFileLocator
{
    public static string? Locate(string unmanagedPath)
    {
        if (File.Exists(unmanagedPath)) return unmanagedPath;
        var managedPath = Path.Combine(
            Path.GetDirectoryName(unmanagedPath) ?? "",
            Path.GetFileNameWithoutExtension(unmanagedPath) + "_managed" + Path.GetExtension(unmanagedPath));
        return File.Exists(managedPath) ? managedPath : null;
    }
}
