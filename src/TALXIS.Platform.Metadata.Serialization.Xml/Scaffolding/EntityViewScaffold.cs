using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-entity-view template post-action script.
/// The template renders the SavedQuery file with a braced GUID filename and savedqueryid;
/// this normalizes any view file an older template engine left unbraced.
/// Transitional adapter: patches files directly for byte-compatible output with the old
/// script; moves onto the typed workspace model as the manipulation API lands.
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
