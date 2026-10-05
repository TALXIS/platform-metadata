using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Element insert/remove helpers that keep the indentation of a hand-edited document intact, so patching
/// a loaded <see cref="XDocument"/> changes only the lines the model changed.
/// </summary>
// TODO: XmlWorkspaceWriter.AddChildElementPreservingWhitespace / ReplaceChildElementsPreservingWhitespace
// implement the same idea with less indent detection; converge both writers on this class.
internal static class XmlPatch
{
    /// <summary>Inserts <paramref name="element"/> after <paramref name="previous"/>, or before the first <paramref name="name"/> sibling, or appends.</summary>
    public static XElement Insert(XElement container, string name, XElement? previous, XElement element)
    {
        if (previous is not null)
        {
            previous.AddAfterSelf(element);
            if (LeadingWhitespace(previous) is { } indent) previous.AddAfterSelf(new XText(indent));
            return element;
        }

        var first = container.Elements(name).FirstOrDefault();
        if (first is null) return Append(container, element);

        first.AddBeforeSelf(element);
        if (LeadingWhitespace(element) is { } firstIndent) first.AddBeforeSelf(new XText(firstIndent));
        return element;
    }

    /// <summary>Appends <paramref name="element"/> as the last child, indenting it like its siblings (or one level deeper than an empty container).</summary>
    public static XElement Append(XElement container, XElement element)
    {
        var last = container.Elements().LastOrDefault();
        if (last is not null)
        {
            last.AddAfterSelf(element);
            if (LeadingWhitespace(last) is { } indent) last.AddAfterSelf(new XText(indent));
            return element;
        }

        var outer = LeadingWhitespace(container);
        if (outer is null || container.Nodes().Any(n => !IsWhitespace(n)))
        {
            container.Add(element);
            return element;
        }

        var unit = outer.IndexOf('\t') >= 0 ? "\t" : "  ";
        container.RemoveNodes();
        container.Add(new XText(outer + unit), element, new XText(outer));
        return element;
    }

    /// <summary>Removes <paramref name="element"/> together with the whitespace that indented it.</summary>
    public static void Remove(XElement element)
    {
        if (element.PreviousNode is { } previous && IsWhitespace(previous)) previous.Remove();
        element.Remove();
    }

    /// <summary>Replaces every <paramref name="name"/> child with one element per value.</summary>
    public static void ReplaceValues(XElement container, string name, IEnumerable<string> values)
    {
        var list = values.ToList();
        foreach (var element in container.Elements(name).ToList()) Remove(element);
        foreach (var value in list) Append(container, new XElement(name, value));
    }

    /// <summary>The whitespace text node (containing a line break) directly before <paramref name="element"/>, or <c>null</c>.</summary>
    public static string? LeadingWhitespace(XElement element) =>
        element.PreviousNode is XText text && IsWhitespace(text) && text.Value.IndexOf('\n') >= 0 ? text.Value : null;

    public static bool IsWhitespace(XNode node) => node is XText text && node is not XCData && string.IsNullOrWhiteSpace(text.Value);
}
