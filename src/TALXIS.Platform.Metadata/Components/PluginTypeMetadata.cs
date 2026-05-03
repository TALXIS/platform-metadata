namespace TALXIS.Platform.Metadata.Components;

public sealed class PluginTypeMetadata : MetadataBase
{
    public required string PluginTypeId { get; set; }
    public string? Name { get; set; }
    public string? AssemblyQualifiedName { get; set; }
    public string? FriendlyName { get; set; }
    public string? WorkflowActivityGroupName { get; set; }
    public string? TypeName { get; set; }
}
