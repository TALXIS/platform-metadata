using System.IO.Compression;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Controls;
using TALXIS.Platform.Metadata.Serialization.Xml.Controls;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Solutions;

/// <summary>
/// Reads a packed solution zip into a <see cref="Solution"/>: the manifest and publisher from
/// <c>solution.xml</c>, and every custom control with its parsed <c>ControlManifest.xml</c>,
/// named from <c>customizations.xml</c> (falling back to the <c>Controls/</c> folder name).
/// </summary>
public static class SolutionArchiveReader
{
    public static Solution Read(Stream zipStream)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        return Read(archive);
    }

    public static Solution Read(ZipArchive archive)
    {
        XElement? manifestElement = null;
        var declaredNames = new List<string>();
        var controls = new List<(ControlManifestInfo Manifest, string EntryPath)>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Equals("solution.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var entryStream = entry.Open();
                manifestElement = XDocument.Load(entryStream).Root?.Element("SolutionManifest");
            }
            else if (entry.Name.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var entryStream = entry.Open();
                declaredNames.AddRange(ReadCustomControlNames(entryStream));
            }
            else if (entry.Name.Equals("ControlManifest.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var entryStream = entry.Open();
                controls.Add((ControlManifestXmlReader.Parse(entryStream), entry.FullName));
            }
        }

        var solution = CreateSolution(manifestElement);
        foreach (var (manifest, entryPath) in controls)
        {
            solution.AddControl(new CustomControl
            {
                Name = ResolveControlName(manifest, entryPath, declaredNames),
                Manifest = manifest,
            });
        }

        return solution;
    }

    private static Solution CreateSolution(XElement? manifest)
    {
        var solution = new Solution
        {
            UniqueName = manifest?.Element("UniqueName")?.Value ?? "Unknown",
            Version = manifest?.Element("Version")?.Value ?? "1.0.0.0",
            ManagedValue = manifest?.Element("Managed")?.Value ?? "0",
        };

        var publisher = manifest?.Element("Publisher");
        if (publisher != null)
        {
            solution.Publisher = new Publisher
            {
                UniqueName = publisher.Element("UniqueName")?.Value ?? "Unknown",
                Prefix = publisher.Element("CustomizationPrefix")?.Value ?? "new",
            };
        }

        return solution;
    }

    private static string? ResolveControlName(ControlManifestInfo manifest, string entryPath, List<string> declaredNames)
    {
        // The solution declares the prefixed name; match on the qualified-name suffix.
        var suffix = "_" + manifest.QualifiedName;
        var declared = declaredNames.FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (declared != null)
            return declared;

        // Fall back to the folder name in the solution layout (Controls/<prefixed-name>/ControlManifest.xml).
        var folder = Path.GetDirectoryName(entryPath)?.Replace('\\', '/').Split('/').LastOrDefault();
        if (!string.IsNullOrEmpty(folder) && folder!.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return folder;

        return null;
    }

    private static IEnumerable<string> ReadCustomControlNames(Stream customizationsStream)
    {
        var doc = XDocument.Load(customizationsStream);
        return doc.Descendants("CustomControl")
            .Select(c => c.Element("Name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }
}
