namespace TALXIS.Platform.Metadata.Validation;

/// <summary>Picks the closest existing name for "did you mean" hints in validation messages.</summary>
internal static class NameSuggestion
{
    /// <summary>
    /// The candidate nearest to <paramref name="name"/> by case-insensitive edit distance, or <c>null</c> when none is within
    /// <paramref name="maxDistance"/> (by default a third of the name's length, at least 2).
    /// </summary>
    public static string? Nearest(string name, IEnumerable<string> candidates, int? maxDistance = null)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var limit = maxDistance ?? Math.Max(2, name.Length / 3);
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate) || string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) continue;
            if (Math.Abs(candidate.Length - name.Length) > limit) continue;

            var distance = Distance(name, candidate);
            if (distance <= limit && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>" Did you mean 'x'?" for the nearest candidate, or an empty string.</summary>
    public static string Hint(string name, IEnumerable<string> candidates, int? maxDistance = null)
    {
        var nearest = Nearest(name, candidates, maxDistance);
        return nearest == null ? "" : $" Did you mean '{nearest}'?";
    }

    private static int Distance(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
