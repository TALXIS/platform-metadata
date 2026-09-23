namespace TALXIS.Platform.Metadata.Merging;

/// <summary>
/// Tree search helpers for <see cref="MergeableNode"/>.
/// </summary>
public static class MergeableNodeExtensions
{
    /// <summary>
    /// Enumerates all descendant nodes in document order (excluding the node itself).
    /// </summary>
    public static IEnumerable<MergeableNode> Descendants(this MergeableNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Returns the first descendant matching the predicate, or null.
    /// </summary>
    public static MergeableNode? FindNode(this MergeableNode node, Func<MergeableNode, bool> predicate) =>
        node.Descendants().FirstOrDefault(predicate);

    /// <summary>
    /// Compares two trees by name, action, text, attributes (order-independent) and children (order-dependent).
    /// </summary>
    public static bool StructurallyEquals(this MergeableNode node, MergeableNode? other)
    {
        if (other == null) return false;
        if (node.Name != other.Name || node.Action != other.Action || node.TextContent != other.TextContent) return false;
        if (node.Attributes.Count != other.Attributes.Count || node.Children.Count != other.Children.Count) return false;

        foreach (var attribute in node.Attributes)
        {
            if (!other.Attributes.TryGetValue(attribute.Key, out var value) || value != attribute.Value) return false;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!node.Children[i].StructurallyEquals(other.Children[i])) return false;
        }

        return true;
    }
}
