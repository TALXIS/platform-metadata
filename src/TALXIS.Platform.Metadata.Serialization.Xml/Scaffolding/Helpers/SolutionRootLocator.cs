using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Resolves the unpacked solution root for a scaffold request. If the supplied
/// path does not identify an existing directory, detects the root from the base
/// directory: Other/Solution.xml in the base itself, the SolutionRootPath property
/// of the single .csproj, or a unique Other/Solution.xml below the base.
/// </summary>
public static class SolutionRootLocator
{
    public static string Resolve(string? requested, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var requestedPath = Path.IsPathRooted(requested) ? requested : Path.Combine(baseDirectory, requested);
            if (Directory.Exists(requestedPath)) return requested;
        }

        if (IsSolutionRoot(baseDirectory)) return baseDirectory;

        var fromProject = FromProjectProperty(baseDirectory);
        if (fromProject != null) return Path.Combine(baseDirectory, fromProject);

        return FromDirectorySearch(baseDirectory);
    }

    private static bool IsSolutionRoot(string directory) =>
        File.Exists(Path.Combine(directory, "Other", "Solution.xml"));

    private static string? FromProjectProperty(string baseDirectory)
    {
        var projects = Directory.Exists(baseDirectory)
            ? Directory.GetFiles(baseDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        if (projects.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple .csproj files found in '{baseDirectory}': {string.Join(", ", projects.Select(Path.GetFileName))}");
        }
        if (projects.Length == 0) return null;

        var doc = new XmlDocument();
        try
        {
            doc.Load(projects[0]);
        }
        catch (XmlException)
        {
            return null;
        }

        var node = doc.SelectSingleNode(
            "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='SolutionRootPath']");
        var value = node?.InnerText.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string FromDirectorySearch(string baseDirectory)
    {
        var candidates = Directory.Exists(baseDirectory)
            ? Directory.GetFiles(baseDirectory, "Solution.xml", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "Other", StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : Array.Empty<string>();

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple Other/Solution.xml files found under '{baseDirectory}':{Environment.NewLine}{string.Join(Environment.NewLine, candidates)}");
        }
        if (candidates.Length == 0)
            throw new InvalidOperationException($"Failed to determine SolutionRootPath from '{baseDirectory}'.");

        return Path.GetDirectoryName(Path.GetDirectoryName(candidates[0]))!;
    }
}
