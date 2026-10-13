using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Shared file-enumeration rules for validators that scan a workspace tree.
/// </summary>
internal static class WorkspaceFiles
{
    internal static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".github", "node_modules", "packages", "TestResults"
    };

    internal static IEnumerable<string> Enumerate(string directory, string pattern)
    {
        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
        {
            if (RelativePathParts(directory, file).Any(p => IgnoredDirectories.Contains(p)))
                continue;
            yield return file;
        }
    }

    /// <summary>
    /// Whether a file has the layout SolutionPackager gives solution component instances (solution-aware table
    /// records and SCF components):
    /// <c>&lt;folder&gt;/&lt;key&gt;/&lt;name&gt;.xml</c> directly under <paramref name="directory"/>, with root element
    /// <c>&lt;name&gt;</c> carrying the component key as an attribute whose value is <c>&lt;key&gt;</c>. The root element
    /// and columns come from the table definition (Entity.xml), so no embedded XSD declares these files.
    /// </summary>
    internal static bool HasComponentInstanceShape(string directory, string filePath, XElement root)
    {
        var parts = RelativePathParts(directory, filePath);
        if (parts.Length != 3)
            return false;

        var key = parts[1];
        return string.Equals(Path.GetFileNameWithoutExtension(parts[2]), root.Name.LocalName, StringComparison.OrdinalIgnoreCase)
            && root.Attributes().Any(a => !a.IsNamespaceDeclaration && string.Equals(a.Value, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] RelativePathParts(string directory, string filePath)
    {
        var fullDir = Path.GetFullPath(directory);
        var relativePath = Path.GetFullPath(filePath).Substring(fullDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static bool IsWebResourcePayload(string filePath)
    {
        if (filePath.EndsWith(".data.xml", StringComparison.OrdinalIgnoreCase)) return false;
        var normalized = filePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return normalized.IndexOf($"{Path.DirectorySeparatorChar}WebResources{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
