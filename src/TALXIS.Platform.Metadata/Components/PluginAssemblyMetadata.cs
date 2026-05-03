namespace TALXIS.Platform.Metadata.Components;

public sealed class PluginAssemblyMetadata : MetadataBase
{
    public required string PluginAssemblyId { get; set; }
    public string? FullName { get; set; }
    public string? Name { get; set; }
    public int? IsolationMode { get; set; }
    public int? SourceType { get; set; }
    public string? FileName { get; set; }
    public string? IntroducedVersion { get; set; }
    public int? CustomizationLevel { get; set; }

    private readonly List<PluginTypeMetadata> _pluginTypes = new();
    public IReadOnlyList<PluginTypeMetadata> PluginTypes => _pluginTypes;

    public void AddPluginType(PluginTypeMetadata pluginType) => _pluginTypes.Add(pluginType);
}
