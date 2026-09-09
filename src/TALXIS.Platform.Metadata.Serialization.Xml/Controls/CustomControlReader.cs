using TALXIS.Platform.Metadata.Controls;
using TALXIS.Platform.Metadata.Serialization.Xml.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Controls;

/// <summary>
/// Resolves a single custom control from any supported source: a bare manifest file, a control
/// project (folder or <c>.csproj</c>), or a distribution archive (solution zip, pdpkg.zip,
/// nupkg). Archive sources also carry the publisher-prefixed control name.
/// </summary>
public static class CustomControlReader
{
    public static CustomControlMetadata Read(string path)
    {
        if (Directory.Exists(path))
            return FromManifest(ControlManifestXmlReader.ReadProject(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Control source not found: {path}");

        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return FromManifest(ControlManifestXmlReader.ReadProject(Path.GetDirectoryName(Path.GetFullPath(path))!));

        if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return FromManifest(ControlManifestXmlReader.ReadFile(path));

        var controls = SolutionPackageReader.Read(path).SelectMany(s => s.Controls).ToList();
        if (controls.Count == 0)
            throw new InvalidOperationException($"No custom control found inside '{path}'.");
        if (controls.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple custom controls found inside '{path}': " +
                $"{string.Join(", ", controls.Select(c => c.Name ?? c.Manifest.QualifiedName))}. " +
                "Pass the control's manifest or project directly.");
        }

        return controls[0];
    }

    private static CustomControlMetadata FromManifest(ControlManifestInfo manifest) => new() { Manifest = manifest };
}
