namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Resolves component files that may exist in a managed variant: given
/// ".../AppModuleSiteMap.xml" it returns that path or the "_managed" sibling,
/// null when neither exists (the old scripts' dual-candidate lookup).
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
