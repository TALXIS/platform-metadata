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
}
