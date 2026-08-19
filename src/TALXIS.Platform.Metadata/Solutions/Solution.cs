using TALXIS.Platform.Metadata.Controls;

namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// In-memory representation of <c>Solution.xml</c>.
/// </summary>
public sealed class Solution : MetadataBase, ILocalizedMetadata
{
    private string _managedValue = "0";

    /// <summary>
    /// Gets or sets the unique solution name.
    /// </summary>
    public required string UniqueName { get; set; }

    /// <summary>
    /// Gets or sets the solution version.
    /// </summary>
    public string Version { get; set; } = "1.0.0.0";

    /// <summary>
    /// Gets or sets whether the solution is managed.
    /// This is a convenience wrapper over <see cref="ManagedValue"/>.
    /// </summary>
    public bool IsManaged
    {
        get => !string.Equals(ManagedValue, "0", StringComparison.Ordinal);
        set => ManagedValue = value ? "1" : "0";
    }

    /// <summary>
    /// Gets or sets the raw managed-state value as stored by SolutionPackager.
    /// <c>"0"</c> means unmanaged; other values are treated as managed.
    /// </summary>
    public string ManagedValue
    {
        get => _managedValue;
        set => _managedValue = string.IsNullOrWhiteSpace(value) ? "0" : value;
    }

    /// <summary>
    /// Gets or sets the publisher metadata for the solution.
    /// </summary>
    public Publisher? Publisher { get; set; }

    /// <inheritdoc />
    public Label DisplayName { get; set; } = new();

    /// <inheritdoc />
    public Label Description { get; set; } = new();

    private readonly List<CustomControl> _controls = new();

    /// <summary>
    /// Gets the custom controls contained in the solution. Populated only when the solution is
    /// read from a packed archive; a read-only convenience view that is not written back by the
    /// workspace writer and does not participate in solution layering.
    /// </summary>
    public IReadOnlyList<CustomControl> Controls => _controls;

    /// <summary>
    /// Adds a custom control to the solution.
    /// </summary>
    /// <param name="control">Control to add.</param>
    /// <exception cref="InvalidOperationException">A control with the same name is already present.</exception>
    public void AddControl(CustomControl control)
    {
        var key = control.Name ?? control.Manifest.QualifiedName;
        if (_controls.Any(c => string.Equals(c.Name ?? c.Manifest.QualifiedName, key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A custom control with name '{key}' already exists in the solution.");
        _controls.Add(control);
    }

    private readonly List<RootComponent> _rootComponents = new();

    /// <summary>
    /// Gets the root components declared by the solution.
    /// </summary>
    public IReadOnlyList<RootComponent> RootComponents => _rootComponents;

    /// <summary>
    /// Adds a root component to the solution manifest.
    /// </summary>
    /// <param name="component">Root component to add.</param>
    public void AddRootComponent(RootComponent component)
    {
        _rootComponents.Add(component);
    }

    /// <summary>
    /// Removes all root components matching the supplied type code and schema name.
    /// </summary>
    /// <param name="type">Dataverse component type.</param>
    /// <param name="schemaName">Schema name used for name-based matching.</param>
    public void RemoveRootComponent(ComponentType type, string? schemaName)
    {
        _rootComponents.RemoveAll(c => c.Type == type
            && string.Equals(c.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes all root components of the supplied type matching either the schema name or the ID.
    /// </summary>
    /// <param name="type">Dataverse component type.</param>
    /// <param name="schemaName">Schema name used for name-based matching; ignored when null.</param>
    /// <param name="id">Component ID used for GUID-based matching; ignored when null.</param>
    public void RemoveRootComponent(ComponentType type, string? schemaName, Guid? id)
    {
        _rootComponents.RemoveAll(c => c.Type == type
            && ((schemaName != null && string.Equals(c.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase))
                || (id.HasValue && c.Id == id)));
    }

    /// <summary>
    /// Finds the first root component matching the supplied type and schema name.
    /// </summary>
    /// <param name="type">Dataverse component type.</param>
    /// <param name="schemaName">Schema name used for name-based matching.</param>
    /// <returns>The matching root component, or <see langword="null"/> when none is present.</returns>
    public RootComponent? FindRootComponent(ComponentType type, string? schemaName)
    {
        return _rootComponents.FirstOrDefault(c => c.Type == type
            && string.Equals(c.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase));
    }
}
