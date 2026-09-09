using System.IO.Compression;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Solutions;

/// <summary>
/// Extracts solutions from a distribution archive: a bare solution zip, a Package Deployer
/// <c>.pdpkg.zip</c>, or a NuGet <c>.nupkg</c>. Nested archives are searched recursively,
/// so a nupkg wrapping a pdpkg wrapping a solution works.
/// </summary>
public static class SolutionPackageReader
{
    private const int MaxArchiveDepth = 3;

    public static IReadOnlyList<Solution> Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static IReadOnlyList<Solution> Read(Stream stream)
    {
        return ReadCore(stream, MaxArchiveDepth);
    }

    private static List<Solution> ReadCore(Stream stream, int depthBudget)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        if (IsSolutionArchive(archive))
            return [SolutionArchiveReader.Read(archive)];

        var solutions = new List<Solution>();
        if (depthBudget <= 0)
            return solutions;

        foreach (var entry in archive.Entries)
        {
            if (!IsNestedArchive(entry.Name))
                continue;
            using var entryStream = entry.Open();
            using var buffered = CopyToMemory(entryStream);
            solutions.AddRange(ReadCore(buffered, depthBudget - 1));
        }

        return solutions;
    }

    private static bool IsSolutionArchive(ZipArchive archive)
    {
        return archive.Entries.Any(e =>
            e.Name.Equals("solution.xml", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals("ControlManifest.xml", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNestedArchive(string name) =>
        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase);

    // ZipArchive entry streams are not seekable, but nested ZipArchive needs a seekable stream.
    private static MemoryStream CopyToMemory(Stream source)
    {
        var memory = new MemoryStream();
        source.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
