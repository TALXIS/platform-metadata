namespace TALXIS.Platform.Metadata.Solutions;

public sealed class Publisher : MetadataBase
{
    public required string UniqueName { get; set; }
    public required string Prefix { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();
    public int? OptionValuePrefix { get; set; }
}
