namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Dataverse-style layer category for a component instance.
/// </summary>
public enum SolutionLayerKind
{
    /// <summary>
    /// A managed solution layer ordered by import order.
    /// </summary>
    Managed = 0,

    /// <summary>
    /// The shared unmanaged active layer that sits above managed layers.
    /// </summary>
    Active = 1,

    /// <summary>
    /// System/base layer, reserved for live-environment projections.
    /// </summary>
    System = 2,

    /// <summary>
    /// Default solution container, reserved for live-environment projections.
    /// </summary>
    Default = 3
}
