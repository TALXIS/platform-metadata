namespace TALXIS.Platform.Metadata;

/// <summary>
/// Location in a source file where a metadata element was defined.
/// Set by serializers during load, used by language servers for diagnostic mapping.
/// </summary>
public sealed record SourceLocation(string FilePath, int Line, int Column);

/// <summary>
/// Base class for all metadata elements. Carries optional source tracking.
/// </summary>
public abstract class MetadataBase
{
    /// <summary>
    /// Where this element was loaded from. Null for in-memory-created elements.
    /// </summary>
    public SourceLocation? Source { get; set; }
}
