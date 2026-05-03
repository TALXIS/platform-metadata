using System.Xml.Linq;
using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Converts between XElement trees and MergeableNode trees.
/// This is the bridge between XML serialization and the format-agnostic merge engine.
/// </summary>
public static class MergeableNodeXmlConverter
{
    private const string SolutionAction = "solutionaction";

    /// <summary>
    /// Converts an XElement tree to a MergeableNode tree.
    /// The "solutionaction" attribute is mapped to <see cref="MergeableNode.Action"/>.
    /// </summary>
    public static MergeableNode FromXElement(XElement element)
    {
        var node = new MergeableNode
        {
            Name = element.Name.LocalName
        };

        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == SolutionAction)
            {
                node.Action = attr.Value switch
                {
                    "Added" => MergeAction.Added,
                    "Removed" => MergeAction.Removed,
                    "Modified" => MergeAction.Modified,
                    _ => null
                };
            }
            else
            {
                node.Attributes[attr.Name.LocalName] = attr.Value;
            }
        }

        if (!element.HasElements && !string.IsNullOrEmpty(element.Value))
        {
            node.TextContent = element.Value;
        }

        foreach (var child in element.Elements())
        {
            node.Children.Add(FromXElement(child));
        }

        return node;
    }

    /// <summary>
    /// Converts a MergeableNode tree back to an XElement tree.
    /// <see cref="MergeableNode.Action"/> is mapped to "solutionaction" attribute.
    /// </summary>
    public static XElement ToXElement(MergeableNode node)
    {
        var element = new XElement(node.Name);

        foreach (var kvp in node.Attributes)
        {
            element.SetAttributeValue(kvp.Key, kvp.Value);
        }

        if (node.Action != null)
        {
            var actionStr = node.Action switch
            {
                MergeAction.Added => "Added",
                MergeAction.Removed => "Removed",
                MergeAction.Modified => "Modified",
                _ => null
            };
            if (actionStr != null)
            {
                element.SetAttributeValue(SolutionAction, actionStr);
            }
        }

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                element.Add(ToXElement(child));
            }
        }
        else if (node.TextContent != null)
        {
            element.Value = node.TextContent;
        }

        return element;
    }
}
