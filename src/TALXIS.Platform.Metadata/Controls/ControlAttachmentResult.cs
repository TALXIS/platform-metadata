namespace TALXIS.Platform.Metadata.Controls;

public sealed class ControlAttachmentResult
{
    public required string ControlName { get; init; }
    public required string HostControlUniqueId { get; init; }
    public bool ReplacedExisting { get; init; }
}
