namespace TALXIS.Platform.Metadata.ConfigurationMigration;

/// <summary>
/// The lookups between the entities a data_schema.xml declares, shared by the import-order resolver and the TXM026 check so both
/// agree on what a lookup cycle is.
/// </summary>
internal static class CmtLookupGraph
{
    /// <summary>
    /// For each of <paramref name="names"/>, the declared entities it looks up (<see cref="CmtSchemaEntity.ReferencedEntities"/>),
    /// limited to <paramref name="names"/>.
    /// </summary>
    public static Dictionary<string, HashSet<string>> Parents(CmtDataSchema schema, IEnumerable<string> names)
    {
        var declared = new HashSet<string>(names, StringComparer.Ordinal);
        var parents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in declared)
            parents[name] = new HashSet<string>(schema.FindEntity(name)!.ReferencedEntities().Where(declared.Contains), StringComparer.Ordinal);

        return parents;
    }

    /// <summary>
    /// Everything <paramref name="entity"/> waits on: its parents, their parents and so on, skipping entities in <paramref name="imported"/>.
    /// </summary>
    public static HashSet<string> Ancestors(string entity, Dictionary<string, HashSet<string>> parents, ICollection<string>? imported = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(parents[entity].Where(p => imported == null || !imported.Contains(p)));
        while (stack.Count > 0)
        {
            var name = stack.Pop();
            if (!seen.Add(name)) continue;
            foreach (var parent in parents[name].Where(p => imported == null || !imported.Contains(p)))
                stack.Push(parent);
        }

        return seen;
    }

    /// <summary>
    /// Whether <paramref name="first"/> and <paramref name="second"/> look each other up, directly or through other entities.
    /// </summary>
    public static bool InOneCycle(string first, string second, Dictionary<string, HashSet<string>> parents)
    {
        if (!parents.ContainsKey(first) || !parents.ContainsKey(second)) return false;

        return Ancestors(first, parents).Contains(second) && Ancestors(second, parents).Contains(first);
    }
}
