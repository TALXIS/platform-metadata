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
        OrderByOrdinalValue(result);
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
                ApplyRemoved(baseNode, layerChild, layerNode.Children);
            }
            else if (action == MergeAction.Modified)
            {
                ApplyModified(baseNode, layerChild, layerNode.Children);
            }
            else
            {
                // Structural node without action: find matching element and recurse
                var match = FindMatchingChild(baseNode, layerChild, layerNode.Children);
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

        // Ordering is best-effort; precise sibling positioning requires parent context
        // that is not tracked in the current MergeableNode model.
        baseParent.Children.Add(clean);
    }

    private static void ApplyRemoved(MergeableNode baseParent, MergeableNode removedNode, List<MergeableNode>? removedNodeSiblings = null)
    {
        var match = FindMatchingChild(baseParent, removedNode, removedNodeSiblings);
        if (match != null)
        {
            baseParent.Children.Remove(match);
        }
    }

    private static void ApplyModified(MergeableNode baseParent, MergeableNode modifiedNode, List<MergeableNode>? modifiedNodeSiblings = null)
    {
        var match = FindMatchingChild(baseParent, modifiedNode, modifiedNodeSiblings);
        if (match == null)
            return;

        // Update attributes (action is not stored as an attribute)
        foreach (var kvp in modifiedNode.Attributes)
        {
            match.Attributes[kvp.Key] = kvp.Value;
        }

        // Remove attributes that were deleted in the modified node
        var modifiedAttrNames = new HashSet<string>(modifiedNode.Attributes.Keys);
        var toRemove = match.Attributes.Keys.Where(k => !modifiedAttrNames.Contains(k)).ToList();
        foreach (var key in toRemove)
            match.Attributes.Remove(key);

        // Always sync TextContent (including setting to null for removal)
        match.TextContent = modifiedNode.TextContent;

        // Recurse into children for nested changes
        ApplyLayer(match, modifiedNode);
    }

    private static MergeableNode? FindMatchingChild(MergeableNode parent, MergeableNode target, List<MergeableNode>? targetSiblings = null)
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
        foreach (var keySet in ElementMatchKeyRegistry.GetKeySets(name))
        {
            if (!HasAllKeys(target, keySet)) continue;

            foreach (var c in candidates)
            {
                if (HasMatchingKeys(c, target, keySet))
                    return c;
            }
        }

        // Index-based matching for keyless elements (e.g. rows)
        if (targetSiblings != null)
        {
            int targetIndex = 0;
            for (int j = 0; j < targetSiblings.Count; j++)
            {
                if (targetSiblings[j] == target) break;
                if (targetSiblings[j].Name == name) targetIndex++;
            }
            return targetIndex < candidates.Count ? candidates[targetIndex] : null;
        }

        // Last resort: first candidate
        return candidates[0];
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
            var bestMatch = FindBestMatchIndex(baseChildren, mc, matchedBase, modifiedChildren);
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
            bool textChanged = bc.TextContent != modifiedChildren[modIdx].TextContent;

            if (attrsChanged || textChanged)
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

    private static int FindBestMatchIndex(List<MergeableNode> candidates, MergeableNode target, HashSet<int> excluded, List<MergeableNode>? targetParentChildren = null)
    {
        var name = target.Name;

        foreach (var keySet in ElementMatchKeyRegistry.GetKeySets(name))
        {
            if (!HasAllKeys(target, keySet)) continue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (excluded.Contains(i)) continue;
                if (candidates[i].Name != name) continue;
                if (HasMatchingKeys(candidates[i], target, keySet))
                    return i;
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
        {
            // Determine position of target among its same-name siblings in the source
            int targetIndex = 0;
            if (targetParentChildren != null)
            {
                for (int j = 0; j < targetParentChildren.Count; j++)
                {
                    if (targetParentChildren[j] == target) break;
                    if (targetParentChildren[j].Name == target.Name) targetIndex++;
                }
            }
            return targetIndex < sameNameIndices.Count ? sameNameIndices[targetIndex] : -1;
        }

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

    private static bool HasAllKeys(MergeableNode node, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (node.GetAttribute(key) == null)
                return false;
        }

        return true;
    }

    private static bool HasMatchingKeys(MergeableNode candidate, MergeableNode target, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            var targetValue = target.GetAttribute(key);
            if (targetValue == null || candidate.GetAttribute(key) != targetValue)
                return false;
        }

        return true;
    }

    private static void OrderByOrdinalValue(MergeableNode node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            OrderByOrdinalValue(node.Children[i]);
        }

        if (!node.Children.Any(static child => TryGetOrdinalValue(child, out _)))
            return;

        var ordered = node.Children
            .Select(static (child, index) => new
            {
                Child = child,
                Index = index,
                HasOrdinal = TryGetOrdinalValue(child, out var ordinal),
                Ordinal = ordinal
            })
            .OrderBy(static item => item.HasOrdinal ? 0 : 1)
            .ThenBy(static item => item.Ordinal)
            .ThenBy(static item => item.Index)
            .Select(static item => item.Child)
            .ToList();

        node.Children.Clear();
        node.Children.AddRange(ordered);
    }

    private static bool TryGetOrdinalValue(MergeableNode node, out int ordinal)
    {
        return int.TryParse(node.GetAttribute("ordinalvalue"), out ordinal);
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
