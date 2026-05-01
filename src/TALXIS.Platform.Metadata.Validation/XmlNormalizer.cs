using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Normalizes XML documents before validation — e.g., strips whitespace
/// from xsi:nil elements that templates/PAC CLI produce.
/// </summary>
public static class XmlNormalizer
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Strips content from elements marked <c>xsi:nil="true"</c>.
    /// Dataverse exports and templates produce <c>&lt;Elem xsi:nil="true"&gt;\n&lt;/Elem&gt;</c>
    /// which is technically invalid (nil elements must be empty).
    /// </summary>
    public static void NormalizeNilElements(XDocument document)
    {
        if (document.Root == null) return;

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            var nilAttr = element.Attribute(Xsi + "nil");
            if (nilAttr != null &&
                string.Equals(nilAttr.Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                // Remove all child nodes (text, whitespace, etc.) to make the element truly empty.
                element.RemoveNodes();
            }
        }
    }
}
