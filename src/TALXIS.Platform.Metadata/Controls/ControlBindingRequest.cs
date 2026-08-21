namespace TALXIS.Platform.Metadata.Controls;

/// <summary>
/// Request to bind a custom control to an existing form control.
/// </summary>
public sealed class ControlBindingRequest
{
    /// <summary>
    /// FormXml control id of the host control (e.g. <c>subgrid</c>).
    /// </summary>
    public required string TargetControlId { get; init; }

    public required ControlManifestInfo Manifest { get; init; }

    /// <summary>
    /// Publisher-prefixed control name for FormXml (e.g. <c>talxis_TALXIS.PCF.Grid</c>).
    /// </summary>
    public required string ControlName { get; init; }

    /// <summary>
    /// Control parameter values keyed by manifest property name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Replace an existing binding on the same host control instead of failing.
    /// </summary>
    public bool Force { get; init; }
}
