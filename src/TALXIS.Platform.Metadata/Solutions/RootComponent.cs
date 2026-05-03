namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Root-component behavior flags used in <c>solution.xml</c>.
/// </summary>
public enum RootComponentBehavior
{
    /// <summary>
    /// Include the root component without automatically adding subcomponents.
    /// </summary>
    IncludeAsShell = 0,

    /// <summary>
    /// Include the root component together with its subcomponents.
    /// </summary>
    IncludeSubcomponents = 1,

    /// <summary>
    /// Include the root component but explicitly avoid adding subcomponents.
    /// </summary>
    DoNotIncludeSubcomponents = 2
}

/// <summary>
/// Entry in <c>solution.xml</c> describing a component included in a solution.
/// </summary>
public sealed class RootComponent
{
    /// <summary>
    /// Gets or sets the Dataverse component type.
    /// </summary>
    public required ComponentType Type { get; set; }

    /// <summary>
    /// Gets or sets the logical or schema name used for name-based components.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the component identifier used for GUID-backed components.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the raw behavior value serialized in <c>solution.xml</c>.
    /// Use <see cref="BehaviorOption"/> for a strongly typed view.
    /// </summary>
    public int Behavior { get; set; }

    /// <summary>
    /// Gets or sets the strongly typed interpretation of <see cref="Behavior"/>.
    /// </summary>
    public RootComponentBehavior BehaviorOption
    {
        get => Enum.IsDefined(typeof(RootComponentBehavior), Behavior)
            ? (RootComponentBehavior)Behavior
            : RootComponentBehavior.IncludeAsShell;
        set => Behavior = (int)value;
    }
}
