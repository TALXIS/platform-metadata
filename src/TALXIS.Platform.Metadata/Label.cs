namespace TALXIS.Platform.Metadata;

/// <summary>
/// A localized label with support for multiple languages.
/// </summary>
public sealed class Label
{
    private readonly Dictionary<int, string> _labels = new();

    public Label() { }

    public Label(string text, int languageCode = 1033)
    {
        _labels[languageCode] = text;
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

    public override string ToString() => Default ?? "";
}
