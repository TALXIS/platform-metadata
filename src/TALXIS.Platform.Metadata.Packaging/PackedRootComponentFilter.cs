using System.IO.Compression;
using System.Xml;

namespace TALXIS.Platform.Metadata.Packaging;

// SolutionPackagerLib matches root components only against the customizations nodes it knows, so some
// components are always reported as missing even when they were packed:
// - an entity form, nested under Entities/Entity/FormXml
// - an AI Builder model (root component type 401), stored inline under AIModels
internal static class PackedRootComponentFilter
{
    // Element path of each component's id in customizations.xml, by the type name the packager reports.
    private static readonly IReadOnlyDictionary<string, string[]> IdPaths = new Dictionary<string, string[]>
    {
        ["SystemForm"] = ["ImportExportXml", "Entities", "Entity", "FormXml", "forms", "systemform", "formid"],
        ["AIModel"] = ["ImportExportXml", "AIModels", "AIModel", "msdyn_aimodelid"],
    };

    public static SolutionPackagerResult Apply(SolutionPackagerResult result, string zipPath)
    {
        if (!result.HasMissingRootComponents) return result;

        IReadOnlySet<(string Type, string Id)> packed;
        try
        {
            packed = ReadPackedIds(zipPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or XmlException or UnauthorizedAccessException)
        {
            // We can't tell which components were packed, so keep the packager's warnings and say why packed ones may still be listed.
            return new SolutionPackagerResult(
                result.Errors,
                [.. result.Warnings, $"Could not read customizations.xml from '{zipPath}' to check packed root components: {ex.Message}"]);
        }

        if (packed.Count == 0) return result;

        // Rewrite the warning in Warnings itself, so MissingRootComponentWarnings is still derived from (and a subset of) it.
        var warnings = result.Warnings
            .Select(warning => SolutionPackagerResult.IsMissingRootComponentsWarning(warning)
                ? RemovePacked(warning, packed)
                : warning)
            .OfType<string>()
            .ToArray();

        return new SolutionPackagerResult(result.Errors, warnings);
    }

    private static string? RemovePacked(string warning, IReadOnlySet<(string Type, string Id)> packed)
    {
        // If any row is not in the expected format, keep the warning as is rather than lose it.
        if (!MissingRootComponent.TryParseWarning(warning, out var components)) return warning;

        var stillMissing = components
            .Where(component => !packed.Contains((component.Type, NormalizeId(component.Key))))
            .ToArray();

        if (stillMissing.Length == components.Count) return warning;
        if (stillMissing.Length == 0) return null;

        return MissingRootComponent.FormatWarning(stillMissing);
    }

    private static IReadOnlySet<(string Type, string Id)> ReadPackedIds(string zipPath)
    {
        var ids = new HashSet<(string Type, string Id)>(PackedIdComparer.Instance);

        using var archive = ZipFile.OpenRead(zipPath);
        var customizations = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The solution ZIP has no customizations.xml.");

        // customizations.xml can be hundreds of MB, so stream it and only descend into elements on one of the IdPaths.
        var path = new List<string>();
        using var stream = customizations.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true });
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            var depth = reader.Depth;
            path.RemoveRange(depth, path.Count - depth);
            path.Add(reader.LocalName);

            var idType = IdPaths.FirstOrDefault(entry => entry.Value.SequenceEqual(path)).Key;
            if (idType is not null)
            {
                var id = NormalizeId(reader.ReadElementContentAsString());
                if (id.Length > 0) ids.Add((idType, id));
                continue;
            }

            if (!IdPaths.Values.Any(idPath => idPath.Length > path.Count && idPath.Take(path.Count).SequenceEqual(path)))
            {
                reader.Skip();
                continue;
            }

            reader.Read();
        }

        return ids;
    }

    private static string NormalizeId(string? value)
    {
        return Guid.TryParse(value, out var id) ? id.ToString("b") : value?.Trim() ?? string.Empty;
    }

    // Types are compared as the packager reports them; ids ignore case.
    private sealed class PackedIdComparer : IEqualityComparer<(string Type, string Id)>
    {
        public static readonly PackedIdComparer Instance = new();

        public bool Equals((string Type, string Id) x, (string Type, string Id) y)
        {
            return x.Type == y.Type && StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id);
        }

        public int GetHashCode((string Type, string Id) obj)
        {
            return HashCode.Combine(obj.Type, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id));
        }
    }
}
