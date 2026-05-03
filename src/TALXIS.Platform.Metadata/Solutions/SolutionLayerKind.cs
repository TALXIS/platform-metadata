namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Dataverse-style layer category for a component instance.
/// </summary>
public enum SolutionLayerKind
{
    /// <summary>
    /// System/base layer, reserved for live-environment projections.
    /// </summary>
    System = 0,

    /// <summary>
    /// Default solution container, reserved for live-environment projections.
    /// </summary>
    Default = 1,

    /// <summary>
    /// A managed solution layer ordered by import order.
    /// </summary>
    Managed = 2,

    /// <summary>
    /// The shared unmanaged active layer that sits above managed layers.
    /// </summary>
    Active = 3
}
