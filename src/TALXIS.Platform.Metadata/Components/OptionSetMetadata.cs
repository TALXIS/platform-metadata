namespace TALXIS.Platform.Metadata.Components;

public sealed class OptionSetMetadata : MetadataBase
{
    public required string Name { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();
    public bool IsGlobal { get; set; }
    public bool IsCustomOptionSet { get; set; }

    private readonly List<OptionMetadata> _options = new();
    public IReadOnlyList<OptionMetadata> Options => _options;

    public void AddOption(OptionMetadata option) => _options.Add(option);
    public void RemoveOption(int value) => _options.RemoveAll(o => o.Value == value);
}

public sealed class OptionMetadata
{
    public required int Value { get; set; }
    public Label Label { get; set; } = new();
    public Label Description { get; set; } = new();
}

public sealed class BooleanOptionSetMetadata : MetadataBase
{
    public Label TrueLabel { get; set; } = new();
    public Label FalseLabel { get; set; } = new();
}
