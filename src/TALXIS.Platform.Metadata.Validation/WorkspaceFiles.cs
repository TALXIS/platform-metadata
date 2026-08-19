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
        var fullDir = Path.GetFullPath(directory);
        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
        {
            var fullFile = Path.GetFullPath(file);
            var relativePath = fullFile.Substring(fullDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => IgnoredDirectories.Contains(p)))
                continue;
            yield return file;
        }
    }

    internal static bool IsWebResourcePayload(string filePath)
    {
        if (filePath.EndsWith(".data.xml", StringComparison.OrdinalIgnoreCase)) return false;
        var normalized = filePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return normalized.IndexOf($"{Path.DirectorySeparatorChar}WebResources{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
