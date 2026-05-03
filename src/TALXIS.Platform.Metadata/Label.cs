namespace TALXIS.Platform.Metadata;

/// <summary>
/// A localized label with support for multiple languages.
/// </summary>
public sealed class Label
{
    private readonly Dictionary<int, string> _labels = new();

    /// <summary>
    /// Creates an empty label collection.
    /// </summary>
    public Label() { }

    /// <summary>
    /// Creates a label with a single localized value.
    /// </summary>
    /// <param name="text">Localized text to store.</param>
    /// <param name="languageCode">Language code (LCID) for the supplied text.</param>
    public Label(string text, int languageCode = 1033)
    {
        _labels[languageCode] = text;
    }

    /// <summary>
    /// Creates a label with multiple localized values.
    /// </summary>
    /// <param name="localizedLabels">Language-code-to-text pairs to initialize the label with.</param>
    public Label(IEnumerable<KeyValuePair<int, string>> localizedLabels)
    {
        if (localizedLabels is null)
            throw new ArgumentNullException(nameof(localizedLabels));

        foreach (var localizedLabel in localizedLabels)
            _labels[localizedLabel.Key] = localizedLabel.Value;
    }

    /// <summary>Gets/sets the label for a specific language code (LCID).</summary>
    public string? this[int languageCode]
    {
        get => _labels.TryGetValue(languageCode, out var v) ? v : null;
        set
        {
            if (value is null) _labels.Remove(languageCode);
            else _labels[languageCode] = value;
        }
    }

    /// <summary>Gets the label in the default language (English/1033), or the first available.</summary>
    public string? Default => (_labels.TryGetValue(1033, out var v) ? v : null) ?? _labels.Values.FirstOrDefault();

    /// <summary>All language codes with labels.</summary>
    public IReadOnlyDictionary<int, string> LocalizedLabels => _labels;

    /// <summary>
    /// Returns the default label text, or an empty string when no localized value exists.
    /// </summary>
    public override string ToString() => Default ?? "";
}
