namespace TALXIS.Platform.Metadata;

/// <summary>
/// Location in a source file where a metadata element was defined.
/// Set by serializers during load, used by language servers for diagnostic mapping.
/// </summary>
public sealed record SourceLocation(string FilePath, int Line, int Column);

/// <summary>
/// Base class for all metadata elements. Carries optional source tracking and an explicit dirty flag.
/// </summary>
public abstract class MetadataBase
{
    /// <summary>
    /// Where this element was loaded from. Null for in-memory-created elements.
    /// </summary>
    public SourceLocation? Source { get; set; }

    /// <summary>
    /// Whether a write was explicitly requested for this element; serializers also detect changes by comparing the element against its source document.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Forces the next save to write this element even when it compares equal to its source document.
    /// </summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>
    /// Clears the dirty flag; called by serializers after the element was written.
    /// </summary>
    public void AcceptChanges() => IsDirty = false;
}
