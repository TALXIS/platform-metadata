namespace TALXIS.Platform.Metadata.Solutions;

public sealed class Solution : MetadataBase
{
    private string _managedValue = "0";

    public required string UniqueName { get; set; }
    public string Version { get; set; } = "1.0.0.0";
    public bool IsManaged
    {
        get => !string.Equals(ManagedValue, "0", StringComparison.Ordinal);
        set => ManagedValue = value ? "1" : "0";
    }
    public string ManagedValue
    {
        get => _managedValue;
        set => _managedValue = string.IsNullOrWhiteSpace(value) ? "0" : value;
    }
    public Publisher? Publisher { get; set; }
    public Label DisplayName { get; set; } = new();
    public Label Description { get; set; } = new();

    private readonly List<RootComponent> _rootComponents = new();
    public IReadOnlyList<RootComponent> RootComponents => _rootComponents;

    public void AddRootComponent(RootComponent component)
    {
        _rootComponents.Add(component);
    }

    public void RemoveRootComponent(int typeCode, string? schemaName)
    {
        _rootComponents.RemoveAll(c => c.TypeCode == typeCode
            && string.Equals(c.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase));
    }

    public RootComponent? FindRootComponent(int typeCode, string? schemaName)
    {
        return _rootComponents.FirstOrDefault(c => c.TypeCode == typeCode
            && string.Equals(c.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase));
    }
}
