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
    /// <param name="order">Caller-defined import/source order.</param>
    public SolutionWorkspaceSource(string path, int order)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Order = order;
    }

    /// <summary>
    /// Gets the root directory of the unpacked SolutionPackager project.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the caller-defined import/source order.
    /// </summary>
    public int Order { get; }
}
