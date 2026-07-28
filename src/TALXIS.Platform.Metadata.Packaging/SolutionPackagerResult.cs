namespace TALXIS.Platform.Metadata.Packaging;

/// <summary>
/// Errors and warnings the packager emitted during a single Pack or Unpack run.
/// </summary>
public sealed class SolutionPackagerResult
{
    // SolutionPackagerLib reports this case only as a warning message; there is no typed signal.
    internal const string MissingRootComponentsWarningPrefix = "Following root components are not defined in customizations";

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> MissingRootComponentWarnings { get; }

    public bool HasErrors => Errors.Count > 0;

    public bool HasMissingRootComponents => MissingRootComponentWarnings.Count > 0;

    internal SolutionPackagerResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
        MissingRootComponentWarnings = warnings
            .Where(w => w.StartsWith(MissingRootComponentsWarningPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
