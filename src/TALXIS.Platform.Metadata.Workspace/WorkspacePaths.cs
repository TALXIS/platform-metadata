namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Path normalization shared by the in-memory and transactional contexts: one separator, no trailing separator, case-insensitive comparison.
/// </summary>
internal static class WorkspacePaths
{
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static string Normalize(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.Length > 1 ? normalized.TrimEnd(Path.DirectorySeparatorChar) : normalized;
    }

    public static string? Parent(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf(Path.DirectorySeparatorChar);
        return index <= 0 ? null : normalizedPath.Substring(0, index);
    }

    public static bool IsUnder(string normalizedDirectory, string normalizedPath) =>
        normalizedPath.Length > normalizedDirectory.Length
        && normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase)
        && normalizedPath[normalizedDirectory.Length] == Path.DirectorySeparatorChar;

    public static bool IsDirectChild(string normalizedDirectory, string normalizedPath) =>
        IsUnder(normalizedDirectory, normalizedPath)
        && normalizedPath.IndexOf(Path.DirectorySeparatorChar, normalizedDirectory.Length + 1) < 0;

    public static IEnumerable<string> Ancestors(string normalizedPath)
    {
        for (var parent = Parent(normalizedPath); parent != null; parent = Parent(parent))
            yield return parent;
    }

    public static bool MatchesPattern(string fileName, string searchPattern)
    {
        if (searchPattern == "*" || searchPattern == "*.*") return true;

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
