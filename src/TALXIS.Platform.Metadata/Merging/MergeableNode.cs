namespace TALXIS.Platform.Metadata.Merging;

/// <summary>
/// A format-agnostic tree node used for component merging.
/// Represents any hierarchical structure (form tabs/sections, sitemap areas/groups, etc.)
/// without depending on XML or any specific serialization format.
/// </summary>
public sealed class MergeableNode
{
    public string Name { get; set; } = "";

    /// <summary>Key-value attributes for this node (id, name, classid, etc.).</summary>
    public Dictionary<string, string> Attributes { get; } = new();

    /// <summary>Ordered child nodes.</summary>
    public List<MergeableNode> Children { get; } = new();

    /// <summary>Text content (if leaf node).</summary>
    public string? TextContent { get; set; }

    /// <summary>The merge action for this node in a diff layer.</summary>
    public MergeAction? Action { get; set; }

    /// <summary>Gets an attribute value, or null if not present.</summary>
    public string? GetAttribute(string name) =>
        Attributes.TryGetValue(name, out var v) ? v : null;
}

public enum MergeAction
{
    Added,
    Removed,
    Modified
}
