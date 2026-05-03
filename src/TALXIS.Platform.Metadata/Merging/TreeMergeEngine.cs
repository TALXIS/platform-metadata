namespace TALXIS.Platform.Metadata.Merging;

/// <summary>
/// Format-agnostic tree merge engine. Operates on <see cref="MergeableNode"/> trees
/// using the same matching and solutionaction semantics as Dataverse form/sitemap merging.
/// </summary>
public static class TreeMergeEngine
{
    /// <summary>
    /// Applies a customization layer on top of a base tree.
    /// Nodes with <see cref="MergeAction.Added"/> are inserted.
    /// Nodes with <see cref="MergeAction.Removed"/> are deleted.
    /// Nodes with <see cref="MergeAction.Modified"/> have their attributes/children updated.
    /// Returns a deep copy of the base tree with the layer applied.
    /// </summary>
    public static MergeableNode Merge(MergeableNode baseTree, MergeableNode customizationLayer)
    {
        var result = DeepClone(baseTree);
        ApplyLayer(result, customizationLayer);
        return result;
    }

    /// <summary>
    /// Computes a diff layer representing changes from base to modified tree.
    /// Added nodes get <see cref="MergeAction.Added"/>.
    /// Removed nodes get <see cref="MergeAction.Removed"/>.
    /// Modified nodes get <see cref="MergeAction.Modified"/>.
    /// </summary>
    public static MergeableNode ComputeDiff(MergeableNode baseTree, MergeableNode modifiedTree)
    {
        var diff = DeepClone(modifiedTree);
        ComputeDiffRecursive(baseTree, modifiedTree, diff);
        CleanUnchangedLeaves(diff);
        return diff;
    }

    private static void ApplyLayer(MergeableNode baseNode, MergeableNode layerNode)
    {
        foreach (var layerChild in layerNode.Children)
        {
            var action = layerChild.Action;

            if (action == MergeAction.Added)
            {
                ApplyAdded(baseNode, layerChild);
            }
            else if (action == MergeAction.Removed)
            {
                ApplyRemoved(baseNode, layerChild);
            }
            else if (action == MergeAction.Modified)
            {
                ApplyModified(baseNode, layerChild);
            }
            else
            {
                // Structural node without action -- find matching element and recurse
                var match = FindMatchingChild(baseNode, layerChild);
                if (match != null)
                {
                    ApplyLayer(match, layerChild);
                }
            }
        }
    }

    private static void ApplyAdded(MergeableNode baseParent, MergeableNode addedNode)
    {
        var clean = DeepClone(addedNode);
        RemoveActions(clean);

        var parentName = addedNode.Name;
        MergeableNode target = baseParent;

        // If the base doesn't directly contain children of this name,
        // try to find a container that does.
        if (baseParent.Name != addedNode.Name)
        {
            // Find a container in baseParent whose children are siblings of addedNode
            MergeableNode? container = null;
            foreach (var child in baseParent.Children)
            {
                if (child.Name == addedNode.Name)
                {
                    container = child.Children.Count > 0 ? baseParent : null;
                    break;
                }
            }
            // Fall through: insert into baseParent (same as original logic)
        }

        target.Children.Add(clean);
    }

    private static void ApplyRemoved(MergeableNode baseParent, MergeableNode removedNode)
    {
        var match = FindMatchingChild(baseParent, removedNode);
        if (match != null)
        {
            baseParent.Children.Remove(match);
        }
    }

    private static void ApplyModified(MergeableNode baseParent, MergeableNode modifiedNode)
    {
        var match = FindMatchingChild(baseParent, modifiedNode);
        if (match == null)
            return;

        // Update attributes (action is not stored as an attribute)
        foreach (var kvp in modifiedNode.Attributes)
        {
            match.Attributes[kvp.Key] = kvp.Value;
        }

        // Recurse into children for nested changes
        ApplyLayer(match, modifiedNode);
    }

    private static MergeableNode? FindMatchingChild(MergeableNode parent, MergeableNode target)
    {
        var name = target.Name;
        var candidates = new List<MergeableNode>();
        foreach (var child in parent.Children)
        {
            if (child.Name == name)
                candidates.Add(child);
        }

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Try matching by element-specific key attributes
        var keys = GetMatchingKeys(name);
        foreach (var key in keys)
        {
            var targetValue = target.GetAttribute(key);
            if (targetValue != null)
            {
                foreach (var c in candidates)
                {
                    if (c.GetAttribute(key) == targetValue)
                        return c;
                }
            }
        }

        // Composite key for handlers
        if (name == "handler")
        {
            var lib = target.GetAttribute("libraryName");
            var func = target.GetAttribute("functionName");
            if (lib != null && func != null)
            {
                foreach (var c in candidates)
                {
                    if (c.GetAttribute("libraryName") == lib &&
                        c.GetAttribute("functionName") == func)
                        return c;
                }
            }
        }

        // Composite key for events
        if (name == "event")
        {
            var eName = target.GetAttribute("name");
            var app = target.GetAttribute("application");
            if (eName != null)
            {
                foreach (var c in candidates)
                {
                    if (c.GetAttribute("name") == eName &&
                        (app == null || c.GetAttribute("application") == app))
                        return c;
                }
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

    private static void RemoveActions(MergeableNode node)
    {
        node.Action = null;
        foreach (var child in node.Children)
        {
            RemoveActions(child);
        }
    }

    private static void ComputeDiffRecursive(MergeableNode baseNode, MergeableNode modifiedNode, MergeableNode diffNode)
    {
        var baseChildren = baseNode.Children;
        var modifiedChildren = modifiedNode.Children;
        var diffChildren = diffNode.Children;

        var matchedBase = new HashSet<int>();
        var matchedModified = new HashSet<int>();

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
                diffChildren[m].Action = MergeAction.Added;
            }
        }

        // Elements in base but not matched -> Removed (add to diff)
        for (int b = 0; b < baseChildren.Count; b++)
        {
            if (!matchedBase.Contains(b))
            {
                var removed = new MergeableNode { Name = baseChildren[b].Name };
                foreach (var kvp in baseChildren[b].Attributes)
                {
                    removed.Attributes[kvp.Key] = kvp.Value;
                }
                removed.Action = MergeAction.Removed;
                diffNode.Children.Add(removed);
            }
        }

        // Matched pairs -> check if modified, recurse for structural elements
        foreach (var (baseIdx, modIdx) in pairs)
        {
            var bc = baseChildren[baseIdx];
            var mc = modifiedChildren[modIdx];
            var dc = diffChildren[modIdx];

            bool attrsChanged = HasAttributeChanges(bc, mc);

            if (attrsChanged)
            {
                dc.Action = MergeAction.Modified;
            }

            if (bc.Children.Count > 0 && mc.Children.Count > 0)
            {
                ComputeDiffRecursive(bc, mc, dc);
            }
            else if (bc.Children.Count == 0 && mc.Children.Count > 0)
            {
                // Children added where there were none
                foreach (var child in dc.Children)
                {
                    child.Action = MergeAction.Added;
                }
            }
            else if (bc.Children.Count > 0 && mc.Children.Count == 0)
            {
                // All children removed
                foreach (var child in bc.Children)
                {
                    var removed = new MergeableNode { Name = child.Name };
                    foreach (var kvp in child.Attributes)
                    {
                        removed.Attributes[kvp.Key] = kvp.Value;
                    }
                    removed.Action = MergeAction.Removed;
                    dc.Children.Add(removed);
                }
            }
        }
    }

    private static int FindBestMatchIndex(List<MergeableNode> candidates, MergeableNode target, HashSet<int> excluded)
    {
        var name = target.Name;
        var keys = GetMatchingKeys(name);

        foreach (var key in keys)
        {
            var targetValue = target.GetAttribute(key);
            if (targetValue != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != name) continue;
                    if (candidates[i].GetAttribute(key) == targetValue)
                        return i;
                }
            }
        }

        // Composite key for handlers
        if (name == "handler")
        {
            var lib = target.GetAttribute("libraryName");
            var func = target.GetAttribute("functionName");
            if (lib != null && func != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != name) continue;
                    if (candidates[i].GetAttribute("libraryName") == lib &&
                        candidates[i].GetAttribute("functionName") == func)
                        return i;
                }
            }
        }

        // Composite key for events
        if (name == "event")
        {
            var eName = target.GetAttribute("name");
            var app = target.GetAttribute("application");
            if (eName != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (excluded.Contains(i)) continue;
                    if (candidates[i].Name != name) continue;
                    if (candidates[i].GetAttribute("name") == eName &&
                        (app == null || candidates[i].GetAttribute("application") == app))
                        return i;
                }
            }
        }

        // Index-based fallback
        var sameNameIndices = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (excluded.Contains(i)) continue;
            if (candidates[i].Name == name)
                sameNameIndices.Add(i);
        }

        if (sameNameIndices.Count > 0)
            return sameNameIndices[0];

        return -1;
    }

    private static bool HasAttributeChanges(MergeableNode baseNode, MergeableNode modifiedNode)
    {
        if (baseNode.Attributes.Count != modifiedNode.Attributes.Count)
            return true;

        foreach (var kvp in modifiedNode.Attributes)
        {
            if (!baseNode.Attributes.TryGetValue(kvp.Key, out var baseVal) || baseVal != kvp.Value)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes nodes from the diff tree that have no action
    /// and no descendants with an action (they represent unchanged structure).
    /// </summary>
    private static void CleanUnchangedLeaves(MergeableNode node)
    {
        // Process bottom-up
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            CleanUnchangedLeaves(node.Children[i]);
        }

        // Remove children that are unchanged leaves
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (child.Action == null && !HasAnyAction(child))
            {
                node.Children.RemoveAt(i);
            }
        }
    }

    private static bool HasAnyAction(MergeableNode node)
    {
        if (node.Action != null)
            return true;
        foreach (var child in node.Children)
        {
            if (HasAnyAction(child))
                return true;
        }
        return false;
    }

    /// <summary>Creates a deep copy of a node tree.</summary>
    public static MergeableNode DeepClone(MergeableNode source)
    {
        var clone = new MergeableNode
        {
            Name = source.Name,
            TextContent = source.TextContent,
            Action = source.Action
        };

        foreach (var kvp in source.Attributes)
        {
            clone.Attributes[kvp.Key] = kvp.Value;
        }

        foreach (var child in source.Children)
        {
            clone.Children.Add(DeepClone(child));
        }

        return clone;
    }
}
