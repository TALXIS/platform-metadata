namespace TALXIS.Platform.Metadata.Controls;

public sealed class ControlBindingResult
{
    public required string ControlName { get; init; }
    public required string HostControlUniqueId { get; init; }
    public bool ReplacedExisting { get; init; }
}
