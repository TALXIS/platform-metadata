using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Resolves the target section (or tab footer) and row inside a form document,
/// mirroring the id-then-index fallback chain of the form template scripts.
/// </summary>
internal static class FormPlacementResolver
{
    public static XmlNode ResolveTargetSection(XmlDocument formDoc, FormPlacement placement)
    {
        var tab = ResolveByIdOrIndex(formDoc, "//tab", placement.TabId, placement.TabIndex)
            ?? throw new InvalidOperationException("Target tab not found.");

        if (placement.SetToTabFooter)
        {
            var footers = tab.SelectNodes("./tabfooter")!;
            if (footers.Count == 0) throw new InvalidOperationException("Target tab footer not found.");
            return footers[footers.Count - 1]!;
        }

        var column = ResolveByIndexOrLast(tab, "./columns/column", placement.ColumnIndex)
            ?? throw new InvalidOperationException("Target column not found in the selected tab.");
        return ResolveByIdOrIndex(column, "./sections/section", placement.SectionId, placement.SectionIndex)
            ?? throw new InvalidOperationException("Target section not found in the selected column.");
    }

    public static XmlNode ResolveTargetRow(XmlNode section, string? rowIndex) =>
        ResolveByIndexOrLast(section, "./rows/row", rowIndex)
            ?? throw new InvalidOperationException("Target row not found in the selected section.");

    // Id wins; an index is only a fallback when both were given. Neither given = last node.
    private static XmlNode? ResolveByIdOrIndex(XmlNode scope, string xpath, string? id, string? index)
    {
        if (id != null)
        {
            var byId = scope.SelectSingleNode($"{xpath}[@id='{{{id}}}']");
            if (byId != null) return byId;
            return index != null ? ResolveByIndex(scope, xpath, index) : null;
        }
        return ResolveByIndexOrLast(scope, xpath, index);
    }

    private static XmlNode? ResolveByIndexOrLast(XmlNode scope, string xpath, string? index)
    {
        if (index != null) return ResolveByIndex(scope, xpath, index);
        var nodes = scope.SelectNodes(xpath)!;
        return nodes.Count > 0 ? nodes[nodes.Count - 1] : null;
    }

    private static XmlNode? ResolveByIndex(XmlNode scope, string xpath, string index)
    {
        var nodes = scope.SelectNodes(xpath)!;
        var position = int.Parse(index);
        return nodes.Count >= position ? nodes[position - 1] : null;
    }
}
