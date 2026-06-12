using System.Diagnostics;

namespace TALXIS.Platform.Metadata.Packaging;

/// <summary>
/// Options for packing or unpacking a Dataverse solution.
/// </summary>
public sealed class SolutionPackagerOptions
{
    /// <summary>
    /// Whether to pack/unpack as managed or unmanaged.
    /// </summary>
    public bool Managed { get; set; }

    /// <summary>
    /// Trace/error level for SolutionPackager output.
    /// Defaults to <see cref="TraceLevel.Info"/>.
    /// </summary>
    public TraceLevel ErrorLevel { get; set; } = TraceLevel.Info;

    /// <summary>
    /// Path to a mapping XML file that directs SolutionPackager how to
    /// rename / relocate files during pack or unpack.
    /// </summary>
    public string? MappingFilePath { get; set; }

    /// <summary>
    /// Path to a log file for detailed SolutionPackager output.
    /// </summary>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// When <c>true</c>, extract or merge localization resource strings.
    /// </summary>
    public bool Localize { get; set; }

    /// <summary>
    /// Locale code (e.g. "auto", "1033") for the source localization template.
    /// Only used when <see cref="Localize"/> is <c>true</c>.
    /// </summary>
    public string? SourceLocale { get; set; }

    /// <summary>
    /// When <c>true</c>, use unmanaged files as fallback for missing managed files
    /// during a managed pack.
    /// </summary>
    public bool UseUnmanagedFileForMissingManaged { get; set; }

    /// <summary>
    /// Whether to allow deletes during unpack.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowDeletes { get; set; } = true;

    /// <summary>
    /// Whether to allow writes during unpack.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool AllowWrites { get; set; } = true;
}
