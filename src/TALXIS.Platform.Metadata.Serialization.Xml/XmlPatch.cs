using System.Xml;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Element insert/remove helpers that keep the indentation of a hand-edited document intact, so patching
/// a loaded <see cref="XDocument"/> changes only the lines the model changed.
/// </summary>
internal static class XmlPatch
{
    /// <summary>Inserts <paramref name="element"/> after <paramref name="previous"/>, or before the first <paramref name="name"/> sibling, or appends.</summary>
    public static XElement Insert(XElement container, string name, XElement? previous, XElement element)
    {
        if (previous != null)
        {
            previous.AddAfterSelf(element);
            AddIndentAfter(previous, LeadingWhitespace(previous));
            return element;
        }

        var first = container.Elements(name).FirstOrDefault();
        if (first == null) return Append(container, element);

        first.AddBeforeSelf(element);
        var indent = LeadingWhitespace(element);
        if (indent != null) first.AddBeforeSelf(new XText(indent));
        return element;
    }

    /// <summary>Appends <paramref name="element"/> as the last child, indenting it like its siblings (or one level deeper than an empty container).</summary>
    public static XElement Append(XElement container, XElement element)
    {
        var last = container.Elements().LastOrDefault();
        if (last != null)
        {
            last.AddAfterSelf(element);
            AddIndentAfter(last, LeadingWhitespace(last));
            return element;
        }

        var outer = LeadingWhitespace(container);
        if (outer == null || container.Nodes().Any(n => !IsWhitespace(n)))
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
        var previous = element.PreviousNode;
        if (previous != null && IsWhitespace(previous)) previous.Remove();
        element.Remove();
    }

    /// <summary>Replaces every <paramref name="name"/> child with one element per value.</summary>
    public static void ReplaceValues(XElement container, string name, IEnumerable<string> values)
    {
        foreach (var element in container.Elements(name).ToList()) Remove(element);
        foreach (var value in values) Append(container, new XElement(name, value));
    }

    // The whitespace text (containing a line break) directly before the element, or null.
    private static string? LeadingWhitespace(XElement element)
    {
        var previous = element.PreviousNode;
        if (previous == null || !IsWhitespace(previous)) return null;
        var text = ((XText)previous).Value;
        return text.IndexOf('\n') >= 0 ? text : null;
    }

    // CDATA derives from XText but is content, not layout.
    private static bool IsWhitespace(XNode node) =>
        node.NodeType == XmlNodeType.Text && string.IsNullOrWhiteSpace(((XText)node).Value);

    // Inserting after an element puts the new one on the same line; repeating the element's indent restores the layout.
    private static void AddIndentAfter(XElement element, string? indent)
    {
        if (indent != null) element.AddAfterSelf(new XText(indent));
    }
}
