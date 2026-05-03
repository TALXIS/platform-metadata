namespace TALXIS.Platform.Metadata.Components;

public sealed class AppModuleMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata
{
    public required string UniqueName { get; set; }
    public Label DisplayName { get; set; } = new();
    public string? IntroducedVersion { get; set; }
    public string? WebResourceId { get; set; }
    public int? FormFactor { get; set; }
    public int? ClientType { get; set; }
    public int? NavigationType { get; set; }
    public int? StateCode { get; set; }
    public int? StatusCode { get; set; }

    private readonly List<AppModuleComponent> _components = new();
    public IReadOnlyList<AppModuleComponent> Components => _components;

    private readonly List<string> _roleIds = new();
    public IReadOnlyList<string> RoleIds => _roleIds;

    public void AddComponent(AppModuleComponent component) => _components.Add(component);
    public void AddRoleId(string roleId) => _roleIds.Add(roleId);
}

public sealed class AppModuleComponent
{
    public int Type { get; set; }
    public string? SchemaName { get; set; }
    public string? Id { get; set; }
}
