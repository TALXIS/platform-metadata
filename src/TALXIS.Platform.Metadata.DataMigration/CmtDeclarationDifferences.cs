namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// Collects the attributes in which two packages declare the same field or relationship differently, written the way they
/// appear in data_schema.xml, so a merge warning can say exactly what to look at.
/// </summary>
internal sealed class CmtDeclarationDifferences
{
    private readonly List<string> _differences = new();

    /// <summary>
    /// Whether any compared attribute differs.
    /// </summary>
    public bool Any => _differences.Count > 0;

    /// <summary>
    /// Records <paramref name="attribute"/> when the two packages give it different values; <c>null</c> means the attribute is absent.
    /// </summary>
    public CmtDeclarationDifferences Compare(string attribute, string? inFirstPackage, string? inLaterPackage)
    {
        if (inFirstPackage != inLaterPackage) _differences.Add($"{attribute} '{inFirstPackage ?? "absent"}' and '{inLaterPackage ?? "absent"}'");
        return this;
    }

    /// <summary>
    /// Compares a boolean attribute as data_schema.xml writes it (<c>true</c>/<c>false</c>, or absent for <c>null</c>).
    /// </summary>
    public CmtDeclarationDifferences Compare(string attribute, bool? inFirstPackage, bool? inLaterPackage)
    {
        return Compare(attribute, AsXml(inFirstPackage), AsXml(inLaterPackage));
    }

    /// <summary>
    /// Compares the attributes the model does not know, by name, so an extension attribute set differently in two packages is reported too.
    /// </summary>
    public CmtDeclarationDifferences CompareOtherAttributes(IDictionary<string, string> inFirstPackage, IDictionary<string, string> inLaterPackage)
    {
        foreach (var name in inFirstPackage.Keys.Union(inLaterPackage.Keys).OrderBy(n => n, StringComparer.Ordinal))
            Compare(name, inFirstPackage.TryGetValue(name, out var first) ? first : null, inLaterPackage.TryGetValue(name, out var later) ? later : null);

        return this;
    }

    /// <summary>
    /// The differences as one readable list, for example <c>type 'decimal' and 'money', updateCompare 'true' and 'false'</c>.
    /// </summary>
    public override string ToString() => string.Join(", ", _differences);

    private static string? AsXml(bool? value) => value == null ? null : value.Value ? "true" : "false";
}
