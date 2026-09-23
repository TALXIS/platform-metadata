namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// An error encountered while loading a workspace file.
/// </summary>
public sealed class WorkspaceLoadError
{
    /// <summary>
    /// Gets the file that could not be loaded successfully.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the load error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the 1-based line number when the loader could determine one.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets the 1-based column number when the loader could determine one.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// Creates a load error.
    /// </summary>
    public WorkspaceLoadError(string filePath, string message, int? line = null, int? column = null)
    {
        FilePath = filePath;
        Message = message;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Returns a compiler-style message including file and optional line/column.
    /// </summary>
    public override string ToString()
    {
        if (Line.HasValue && Column.HasValue)
            return $"{FilePath}({Line},{Column}): {Message}";

        return $"{FilePath}: {Message}";
    }
}
