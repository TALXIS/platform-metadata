using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Merges form XML layers using solutionaction attributes.
/// Supports the common operations: adding, removing, and modifying
/// tabs, sections, rows, cells, and controls.
/// </summary>
public static class FormXmlMerge
{
    private const string SolutionAction = "solutionaction";
    private const string Added = "Added";
    private const string Removed = "Removed";
    private const string Modified = "Modified";

    /// <summary>
    /// Applies a customization layer on top of a base form.
    /// Elements with solutionaction="Added" are inserted.
    /// Elements with solutionaction="Removed" are deleted.
    /// Elements with solutionaction="Modified" have their attributes/children updated.
    /// </summary>
    public static XDocument Merge(XDocument baseForm, XDocument customizationLayer)
    {
        var result = new XDocument(baseForm);

        if (customizationLayer.Root == null)
            return result;

        if (result.Root == null)
            return result;

        ApplyLayer(result.Root, customizationLayer.Root);
        return result;
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

        var diff = new XDocument(modifiedForm);

        if (baseForm.Root == null)
        {
            MarkAllAdded(diff.Root!);
            return diff;
        }

        ComputeDiffRecursive(baseForm.Root, modifiedForm.Root, diff.Root!);
        CleanUnchangedLeaves(diff.Root!);
        return diff;
    }

    private static void ApplyLayer(XElement baseElement, XElement layerElement)
    {
        foreach (var layerChild in layerElement.Elements())
        {
            var action = layerChild.Attribute(SolutionAction)?.Value;

            if (action == Added)
            {
                ApplyAdded(baseElement, layerChild);
            }
            else if (action == Removed)
            {
                ApplyRemoved(baseElement, layerChild);
            }
            else if (action == Modified)
            {
                ApplyModified(baseElement, layerChild);
            }
            else
            {
                // Structural element without solutionaction -- find matching element and recurse
                var match = FindMatchingElement(baseElement, layerChild);
                if (match != null)
                {
                    ApplyLayer(match, layerChild);
                }
            }
        }
    }

    private static void ApplyAdded(XElement baseParent, XElement addedElement)
    {
        var clean = new XElement(addedElement);
        RemoveSolutionActions(clean);

        // Find the container in base that corresponds to the parent of the added element.
        // The added element should be inserted into the matching container.
        var parentName = addedElement.Parent?.Name.LocalName;
        XElement target = baseParent;

        // If the base element name matches the parent container name from the layer,
        // insert directly. Otherwise find/create the container.
        if (baseParent.Name.LocalName != parentName)
        {
            var container = baseParent.Element(addedElement.Name.LocalName)?.Parent
                            ?? baseParent.Elements(parentName).FirstOrDefault();
            if (container != null)
                target = container;
        }

        target.Add(clean);
    }

    private static void ApplyRemoved(XElement baseParent, XElement removedElement)
    {
        var match = FindMatchingElement(baseParent, removedElement);
        match?.Remove();
    }

    private static void ApplyModified(XElement baseParent, XElement modifiedElement)
    {
        var match = FindMatchingElement(baseParent, modifiedElement);
        if (match == null)
            return;

        // Update attributes (except solutionaction)
        foreach (var attr in modifiedElement.Attributes())
        {
            if (attr.Name.LocalName == SolutionAction)
                continue;
            match.SetAttributeValue(attr.Name, attr.Value);
        }

        // Recurse into children for nested changes
        ApplyLayer(match, modifiedElement);
    }

    private static XElement? FindMatchingElement(XElement parent, XElement target)
    {
        var name = target.Name.LocalName;
        var candidates = parent.Elements(target.Name).ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Try matching by element-specific key attributes
        var keys = GetMatchingKeys(name);
        foreach (var key in keys)
        {
            var targetValue = target.Attribute(key)?.Value;
            if (targetValue != null)
            {
                var match = candidates.FirstOrDefault(c => c.Attribute(key)?.Value == targetValue);
                if (match != null)
                    return match;
            }
        }

        // Fallback: match by composite key for handlers
        if (name == "handler")
        {
            var lib = target.Attribute("libraryName")?.Value;
            var func = target.Attribute("functionName")?.Value;
            if (lib != null && func != null)
            {
                return candidates.FirstOrDefault(c =>
                    c.Attribute("libraryName")?.Value == lib &&
                    c.Attribute("functionName")?.Value == func);
            }
        }

        // Fallback: match by composite key for events
        if (name == "event")
        {
            var eName = target.Attribute("name")?.Value;
            var app = target.Attribute("application")?.Value;
            if (eName != null)
            {
                return candidates.FirstOrDefault(c =>
                    c.Attribute("name")?.Value == eName &&
                    (app == null || c.Attribute("application")?.Value == app));
            }
        }

        // Last resort: first candidate
        return candidates[0];
    }

    private static string[] GetMatchingKeys(string elementName)
    {
        return elementName switch
        {
            "tab" => new[] { "id", "name" },
            "section" => new[] { "id", "name" },
            "cell" => new[] { "id" },
            "control" => new[] { "id", "datafieldname" },
            "row" => Array.Empty<string>(), // index-based
            "event" => new[] { "name" },
            "handler" => new[] { "libraryName" },
            "column" => new[] { "id" },
            "controlDescription" => new[] { "forControl" },
            _ => new[] { "id", "name" }
        };
    }

    private static void RemoveSolutionActions(XElement element)
    {
        element.Attribute(SolutionAction)?.Remove();
        foreach (var desc in element.Descendants())
        {
            desc.Attribute(SolutionAction)?.Remove();
        }
    }

    private static void ComputeDiffRecursive(XElement baseElement, XElement modifiedElement, XElement diffElement)
    {
        var baseName = baseElement.Name.LocalName;
        var baseChildren = baseElement.Elements().ToList();
        var modifiedChildren = modifiedElement.Elements().ToList();
        var diffChildren = diffElement.Elements().ToList();

        // Track which base children have been matched
        var matchedBase = new HashSet<int>();
        var matchedModified = new HashSet<int>();

        // Build match pairs
        var pairs = new List<(int baseIdx, int modIdx)>();

        for (int m = 0; m < modifiedChildren.Count; m++)
        {
            var mc = modifiedChildren[m];
            var bestMatch = FindBestMatchIndex(baseChildren, mc, matchedBase);
            if (bestMatch >= 0)
            {
                pairs.Add((bestMatch, m));
                matchedBase.Add(bestMatch);
                matchedModified.Add(m);
            }
        }

        // Elements in modified but not matched to base -> Added
        for (int m = 0; m < modifiedChildren.Count; m++)
        {
            if (!matchedModified.Contains(m))
            {
                var diffChild = diffChildren[m];
                diffChild.SetAttributeValue(SolutionAction, Added);
            }
        }

        // Elements in base but not matched -> Removed (add to diff)
        for (int b = 0; b < baseChildren.Count; b++)
        {
            if (!matchedBase.Contains(b))
            {
                var removed = new XElement(baseChildren[b]);
                // Strip all children, keep only identifying attributes
                removed.RemoveNodes();
                foreach (var attr in baseChildren[b].Attributes())
                {
                    removed.SetAttributeValue(attr.Name, attr.Value);
                }
                removed.SetAttributeValue(SolutionAction, Removed);
                diffElement.Add(removed);
            }
        }

        // Matched pairs -> check if modified, recurse for structural elements
        foreach (var (baseIdx, modIdx) in pairs)
        {
            var bc = baseChildren[baseIdx];
            var dc = diffChildren[modIdx];

            // Check if attributes differ (excluding solutionaction)
            bool attrsChanged = HasAttributeChanges(bc, modifiedChildren[modIdx]);

            if (attrsChanged)
            {
                dc.SetAttributeValue(SolutionAction, Modified);
            }

            // Recurse into children
            if (bc.HasElements && modifiedChildren[modIdx].HasElements)
            {
                ComputeDiffRecursive(bc, modifiedChildren[modIdx], dc);
            }
            else if (!bc.HasElements && modifiedChildren[modIdx].HasElements)
            {
                // Children added where there were none -> mark all as Added
                foreach (var child in dc.Elements())
                {
                    child.SetAttributeValue(SolutionAction, Added);
                }
            }
            else if (bc.HasElements && !modifiedChildren[modIdx].HasElements)
            {
                // All children removed
                foreach (var child in bc.Elements())
                {
                    var removed = new XElement(child);
                    removed.RemoveNodes();
                    removed.SetAttributeValue(SolutionAction, Removed);
                    dc.Add(removed);
                }
            }
        }
    }

    private static int FindBestMatchIndex(List<XElement> candidates, XElement target, HashSet<int> excluded)
    {
        var name = target.Name.LocalName;
        var keys = GetMatchingKeys(name);

        // Try key-based matching first
        foreach (var key in keys)
        {
            var targetValue = target.Attribute(key)?.Value;
            if (targetValue != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != target.Name) continue;
                    if (candidates[i].Attribute(key)?.Value == targetValue)
                        return i;
                }
            }
        }

        // Composite key for handlers
        if (name == "handler")
        {
            var lib = target.Attribute("libraryName")?.Value;
            var func = target.Attribute("functionName")?.Value;
            if (lib != null && func != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != target.Name) continue;
                    if (candidates[i].Attribute("libraryName")?.Value == lib &&
                        candidates[i].Attribute("functionName")?.Value == func)
                        return i;
                }
            }
        }

        // Composite key for events
        if (name == "event")
        {
            var eName = target.Attribute("name")?.Value;
            var app = target.Attribute("application")?.Value;
            if (eName != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != target.Name) continue;
                    if (candidates[i].Attribute("name")?.Value == eName &&
                        (app == null || candidates[i].Attribute("application")?.Value == app))
                        return i;
                }
            }
        }

        // Index-based fallback for rows or unkeyed elements of same name
        var sameNameIndices = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (excluded.Contains(i)) continue;
            if (candidates[i].Name == target.Name)
                sameNameIndices.Add(i);
        }

        if (sameNameIndices.Count > 0)
            return sameNameIndices[0];

        return -1;
    }

    private static bool HasAttributeChanges(XElement baseEl, XElement modifiedEl)
    {
        var baseAttrs = baseEl.Attributes()
            .Where(a => a.Name.LocalName != SolutionAction)
            .ToDictionary(a => a.Name, a => a.Value);
        var modAttrs = modifiedEl.Attributes()
            .Where(a => a.Name.LocalName != SolutionAction)
            .ToDictionary(a => a.Name, a => a.Value);

        if (baseAttrs.Count != modAttrs.Count)
            return true;

        foreach (var kvp in modAttrs)
        {
            if (!baseAttrs.TryGetValue(kvp.Key, out var baseVal) || baseVal != kvp.Value)
                return true;
        }

        return false;
    }

    private static void MarkAllAdded(XElement element)
    {
        element.SetAttributeValue(SolutionAction, Added);
        foreach (var child in element.Elements())
        {
            MarkAllAdded(child);
        }
    }

    /// <summary>
    /// Removes elements from the diff tree that have no solutionaction
    /// and no descendants with solutionaction (they represent unchanged structure).
    /// Keeps structural containers that have changed descendants.
    /// </summary>
    private static void CleanUnchangedLeaves(XElement element)
    {
        // Process bottom-up
        foreach (var child in element.Elements().ToList())
        {
            CleanUnchangedLeaves(child);
        }

        // Don't remove the root
        if (element.Parent == null)
            return;

        // Keep if it has a solutionaction
        if (element.Attribute(SolutionAction) != null)
            return;

        // Keep if any descendant has a solutionaction
        if (element.Descendants().Any(d => d.Attribute(SolutionAction) != null))
            return;

        // Remove unchanged leaf/branch
        element.Remove();
    }
}
