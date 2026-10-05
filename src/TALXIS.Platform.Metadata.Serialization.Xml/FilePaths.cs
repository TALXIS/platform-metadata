using System.Runtime.InteropServices;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Path comparison that follows the file system's case rules: case-insensitive on Windows, ordinal elsewhere.
/// </summary>
// TODO: XmlWorkspaceWriter.PathsEqual compares case-insensitively on every platform; switch it to this helper.
internal static class FilePaths
{
    public static bool Equal(string left, string right)
    {
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Normalize(left), Normalize(right), comparison);
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
