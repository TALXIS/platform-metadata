namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Describes one SolutionPackager project to load into a multi-solution workspace.
/// </summary>
public sealed class SolutionWorkspaceSource
{
    /// <summary>
    /// Creates a solution workspace source.
    /// </summary>
    /// <param name="path">Root directory of the unpacked SolutionPackager project.</param>
    /// <param name="importOrder">Caller-defined import/source order. Lower values are loaded first.</param>
    public SolutionWorkspaceSource(string path, int importOrder)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Workspace source path is required.", nameof(path));

        Path = path;
        ImportOrder = importOrder;
    }

    /// <summary>
    /// Gets the root directory of the unpacked SolutionPackager project.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the caller-defined import/source order. Lower values are loaded first.
    /// </summary>
    public int ImportOrder { get; }
}
