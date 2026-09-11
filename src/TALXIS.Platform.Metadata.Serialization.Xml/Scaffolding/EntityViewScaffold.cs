using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Normalizes unbraced GUIDs in SavedQuery file names and savedqueryid values
/// to braced GUIDs.
/// </summary>
public static class EntityViewScaffold
{
    public static ScaffoldResult Apply(EntityViewScaffoldRequest request)
    {
        var viewsDirectory = Path.Combine(
            request.SolutionRootPath, SolutionPackagerLayout.EntitiesDirectory, request.EntitySchemaName, "SavedQueries");
        if (!Directory.Exists(viewsDirectory))
            throw new DirectoryNotFoundException($"SavedQueries directory not found: {viewsDirectory}");

        GuidBraceNormalizer.NormalizeFirstUnbraced(viewsDirectory, "//savedqueryid");
        return new ScaffoldResult();
    }
}
