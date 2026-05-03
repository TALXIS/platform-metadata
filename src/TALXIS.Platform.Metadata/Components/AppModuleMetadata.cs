using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Model-driven app module metadata.
/// </summary>
public sealed class AppModuleMetadata : MetadataBase, IDisplayNamedMetadata, IVersionedMetadata
{
    /// <summary>
    /// Gets or sets the app-module unique name.
    /// </summary>
    public required string UniqueName { get; set; }

    /// <inheritdoc />
    public Label DisplayName { get; set; } = new();

    /// <inheritdoc />
    public string? IntroducedVersion { get; set; }

    /// <summary>
    /// Gets or sets the web-resource identifier used by the app icon.
    /// </summary>
    public string? WebResourceId { get; set; }

    /// <summary>
    /// Gets or sets the target form factor.
    /// </summary>
    public int? FormFactor { get; set; }

    /// <summary>
    /// Gets or sets the client type.
    /// </summary>
    public int? ClientType { get; set; }

    /// <summary>
    /// Gets or sets the navigation behavior.
    /// </summary>
    public int? NavigationType { get; set; }

    /// <summary>
    /// Gets or sets the state code.
    /// </summary>
    public int? StateCode { get; set; }

    /// <summary>
    /// Gets or sets the status code.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the mergeable app-module body.
    /// </summary>
    public MergeableNode? Body { get; set; }

    private readonly List<AppModuleComponent> _components = new();

    /// <summary>
    /// Gets the components referenced by the app module.
    /// </summary>
    public IReadOnlyList<AppModuleComponent> Components => _components;

    private readonly List<string> _roleIds = new();

    /// <summary>
    /// Gets the security-role identifiers assigned to the app module.
    /// </summary>
    public IReadOnlyList<string> RoleIds => _roleIds;

    /// <summary>
    /// Adds an app-module component reference.
    /// </summary>
    public void AddComponent(AppModuleComponent component) => _components.Add(component);

    /// <summary>
    /// Adds a security-role identifier.
    /// </summary>
    public void AddRoleId(string roleId) => _roleIds.Add(roleId);
}

/// <summary>
/// Component reference contained inside an app module.
/// </summary>
public sealed class AppModuleComponent
{
    /// <summary>
    /// Gets or sets the Dataverse component type.
    /// </summary>
    public ComponentType Type { get; set; }

    /// <summary>
    /// Gets or sets the schema name when the referenced component is name-based.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the referenced component identifier.
    /// </summary>
    public string? Id { get; set; }
}
