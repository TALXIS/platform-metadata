namespace TALXIS.Platform.Metadata.Merging;

/// <summary>
/// A format-agnostic tree node used for component merging.
/// Represents any hierarchical structure (form tabs/sections, sitemap areas/groups, etc.)
/// without depending on XML or any specific serialization format.
/// </summary>
public sealed class MergeableNode
{
    private readonly Dictionary<string, string> _attributes = new();
    private readonly List<MergeableNode> _children = new();

    /// <summary>
    /// Gets or sets the element or node name used during matching and serialization.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets the node attributes as a read-only map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <summary>
    /// Gets the ordered child nodes.
    /// </summary>
    public IReadOnlyList<MergeableNode> Children => _children;

    /// <summary>Text content (if leaf node).</summary>
    public string? TextContent { get; set; }

    /// <summary>The merge action for this node in a diff layer.</summary>
    public MergeAction? Action { get; set; }

    /// <summary>Gets an attribute value, or null if not present.</summary>
    public string? GetAttribute(string name) =>
        _attributes.TryGetValue(name, out var v) ? v : null;

    /// <summary>
     /// Adds a child node to <see cref="Children"/>.
     /// </summary>
    /// <param name="child">Child node to append.</param>
    public void AddChild(MergeableNode child)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));

        _children.Add(child);
    }

    /// <summary>
    /// Inserts a child node at a specific position.
    /// </summary>
    public void InsertChild(int index, MergeableNode child)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));

        _children.Insert(index, child);
    }

    /// <summary>
    /// Removes a child node.
    /// </summary>
    public bool RemoveChild(MergeableNode child)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));

        return _children.Remove(child);
    }

    /// <summary>
    /// Adds or replaces an attribute value on this node.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Attribute value.</param>
    public void SetAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name cannot be null or whitespace.", nameof(name));

        _attributes[name] = value;
    }

    /// <summary>
    /// Removes an attribute from the node.
    /// </summary>
    public bool RemoveAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name cannot be null or whitespace.", nameof(name));

        return _attributes.Remove(name);
    }

    internal Dictionary<string, string> MutableAttributes => _attributes;
    internal List<MergeableNode> MutableChildren => _children;
}

/// <summary>
/// Kind of change represented by a merge diff node.
/// </summary>
public enum MergeAction
{
    /// <summary>
    /// The node exists only in the customization layer and should be inserted.
    /// </summary>
    Added,

    /// <summary>
    /// The node exists only in the base layer and should be removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The node exists in both layers but its attributes, text, or descendants changed.
    /// </summary>
    Modified
}
