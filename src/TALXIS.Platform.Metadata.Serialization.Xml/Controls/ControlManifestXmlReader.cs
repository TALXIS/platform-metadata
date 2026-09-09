using System.Xml.Linq;
using TALXIS.Platform.Metadata.Controls;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Controls;

/// <summary>
/// Parses a PCF <c>ControlManifest.xml</c> from a stream or file, and locates the source
/// manifest inside a control project folder.
/// </summary>
public static class ControlManifestXmlReader
{
    private static readonly string[] ExcludedProjectDirs = ["bin", "obj", "out", "node_modules", ".git"];

    public static ControlManifestInfo Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var control = doc.Descendants("control").FirstOrDefault()
            ?? throw new InvalidOperationException("Invalid control manifest: no <control> element.");

        var ns = control.Attribute("namespace")?.Value
            ?? throw new InvalidOperationException("Invalid control manifest: <control> has no 'namespace' attribute.");
        var constructor = control.Attribute("constructor")?.Value
            ?? throw new InvalidOperationException("Invalid control manifest: <control> has no 'constructor' attribute.");

        var properties = control.Elements("property")
            .Select(p => new ControlManifestProperty
            {
                Name = p.Attribute("name")?.Value ?? "",
                OfType = p.Attribute("of-type")?.Value ?? p.Attribute("of-type-group")?.Value ?? "SingleLine.Text",
                DefaultValue = p.Attribute("default-value")?.Value,
                Required = string.Equals(p.Attribute("required")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                EnumValues = p.Elements("value").Select(v => v.Value.Trim()).ToList(),
            })
            .Where(p => p.Name.Length > 0)
            .ToList();

        return new ControlManifestInfo
        {
            Namespace = ns,
            Constructor = constructor,
            Version = control.Attribute("version")?.Value,
            DataSets = control.Elements("data-set").Select(d => d.Attribute("name")?.Value ?? "").Where(n => n.Length > 0).ToList(),
            Properties = properties,
        };
    }

    public static ControlManifestInfo ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    public static ControlManifestInfo ReadProject(string directory)
    {
        return ReadFile(FindManifestInProject(Path.GetFullPath(directory)));
    }

    // Source projects name the manifest ControlManifest.Input.xml; build output uses ControlManifest.xml.
    private static string FindManifestInProject(string directory)
    {
        string Relative(string file) =>
            file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var candidates = Directory.EnumerateFiles(directory, "ControlManifest*.xml", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.Equals("ControlManifest.xml", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ControlManifest.Input.xml", StringComparison.OrdinalIgnoreCase);
            })
            .Where(f => !Relative(f).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedProjectDirs.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No ControlManifest.xml found under '{directory}'.");
        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple control manifests found under '{directory}': {string.Join(", ", candidates.Select(Relative))}. " +
                "Pass the manifest file directly.");
        }

        return candidates[0];
    }
}
