namespace TALXIS.Platform.Metadata.Merging;

/// <summary>
/// Provides element-specific key sets used to match mergeable tree nodes across solution layers.
/// Each inner array represents a key set whose attributes must all match.
/// </summary>
public static class ElementMatchKeyRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, IReadOnlyList<IReadOnlyList<string>>> KeySets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tab"] = KeySetsFor(K("id"), K("name"), K("ordinalvalue")),
            ["section"] = KeySetsFor(K("id"), K("name"), K("ordinalvalue")),
            ["cell"] = KeySetsFor(K("id"), K("ordinalvalue")),
            ["control"] = KeySetsFor(K("id"), K("datafieldname")),
            ["row"] = KeySetsFor(K("ordinalvalue")),
            ["event"] = KeySetsFor(K("name", "application"), K("name")),
            ["handler"] = KeySetsFor(K("libraryName", "functionName"), K("libraryName")),
            ["column"] = KeySetsFor(K("id"), K("ordinalvalue")),
            ["controlDescription"] = KeySetsFor(K("forControl")),
            ["Area"] = KeySetsFor(K("Id"), K("ordinalvalue")),
            ["Group"] = KeySetsFor(K("Id"), K("ordinalvalue")),
            ["SubArea"] = KeySetsFor(K("Id"), K("Entity"), K("Url"), K("ordinalvalue")),
            ["AppModuleComponent"] = KeySetsFor(K("id"), K("type", "schemaName"), K("type", "id"), K("ordinalvalue")),
            ["Role"] = KeySetsFor(K("id")),
            ["CustomAction"] = KeySetsFor(K("Id"), K("ordinalvalue")),
            ["CommandDefinition"] = KeySetsFor(K("Id")),
            ["EnableRule"] = KeySetsFor(K("Id")),
            ["DisplayRule"] = KeySetsFor(K("Id")),
            ["TabDisplayRule"] = KeySetsFor(K("TabCommand")),
            ["LocLabel"] = KeySetsFor(K("Id"))
        };

    private static readonly IReadOnlyList<IReadOnlyList<string>> DefaultKeySets =
        KeySetsFor(K("id"), K("Id"), K("name"), K("Name"), K("ordinalvalue"));

    public static IReadOnlyList<IReadOnlyList<string>> GetKeySets(string elementName)
    {
        lock (SyncRoot)
        {
            return KeySets.TryGetValue(elementName, out var keySets)
                ? keySets
                : DefaultKeySets;
        }
    }

    public static void Register(string elementName, params string[][] keySets)
    {
        if (string.IsNullOrWhiteSpace(elementName))
            throw new ArgumentException("Element name must not be empty.", nameof(elementName));
        if (keySets.Length == 0)
            throw new ArgumentException("At least one key set is required.", nameof(keySets));
        if (keySets.Any(static keySet => keySet.Length == 0 || keySet.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Key sets must not contain empty keys.", nameof(keySets));

        lock (SyncRoot)
        {
            KeySets[elementName] = keySets
                .Select(static keySet => keySet.ToArray())
                .ToArray();
        }
    }

    private static string[] K(params string[] keys) => keys;

    private static IReadOnlyList<IReadOnlyList<string>> KeySetsFor(params string[][] keySets) =>
        keySets.Select(static keySet => (IReadOnlyList<string>)keySet).ToArray();
}
