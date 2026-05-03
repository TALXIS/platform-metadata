using System.Xml.Linq;
using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// XML convenience wrapper around <see cref="TreeMergeEngine"/>.
/// Converts XDocument ↔ MergeableNode, delegates merge/diff to the core engine,
/// and converts back. Preserves the original XDocument-based API for callers
/// that work directly with XML.
/// </summary>
public static class FormXmlMerge
{
    /// <summary>
    /// Applies a customization layer on top of a base form.
    /// Elements with solutionaction="Added" are inserted.
    /// Elements with solutionaction="Removed" are deleted.
    /// Elements with solutionaction="Modified" have their attributes/children updated.
    /// </summary>
    public static XDocument Merge(XDocument baseForm, XDocument customizationLayer)
    {
        if (customizationLayer.Root == null || baseForm.Root == null)
            return new XDocument(baseForm);

        var baseTree = MergeableNodeXmlConverter.FromXElement(baseForm.Root);
        var layerTree = MergeableNodeXmlConverter.FromXElement(customizationLayer.Root);

        var result = TreeMergeEngine.Merge(baseTree, layerTree);

        return new XDocument(MergeableNodeXmlConverter.ToXElement(result));
    }

    /// <summary>
    /// Computes a diff layer representing changes from base to modified form.
    /// Added elements get solutionaction="Added".
    /// Removed elements get solutionaction="Removed".
    /// Modified elements get solutionaction="Modified".
    /// </summary>
    public static XDocument ComputeDiff(XDocument baseForm, XDocument modifiedForm)
    {
        if (modifiedForm.Root == null)
            return new XDocument();

        if (baseForm.Root == null)
        {
            var allAdded = MergeableNodeXmlConverter.FromXElement(modifiedForm.Root);
            MarkAllAdded(allAdded);
            return new XDocument(MergeableNodeXmlConverter.ToXElement(allAdded));
        }

        var baseTree = MergeableNodeXmlConverter.FromXElement(baseForm.Root);
        var modifiedTree = MergeableNodeXmlConverter.FromXElement(modifiedForm.Root);

        var diff = TreeMergeEngine.ComputeDiff(baseTree, modifiedTree);

        return new XDocument(MergeableNodeXmlConverter.ToXElement(diff));
    }

    private static void MarkAllAdded(MergeableNode node)
    {
        node.Action = MergeAction.Added;
        foreach (var child in node.Children)
        {
            MarkAllAdded(child);
        }
    }
}
