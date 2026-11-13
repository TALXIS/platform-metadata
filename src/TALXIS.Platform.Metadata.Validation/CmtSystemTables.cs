namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Platform tables every CMT export references (owner, createdby, currency) and that exist in every environment.
/// </summary>
internal static class CmtSystemTables
{
    public static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "systemuser", "team", "businessunit", "transactioncurrency", "organization"
    };

    /// <summary>
    /// Whether <paramref name="name"/> is one of <see cref="Names"/>, whatever its letter case.
    /// </summary>
    public static bool Contains(string name) => Names.Contains(name.ToLowerInvariant());
}
