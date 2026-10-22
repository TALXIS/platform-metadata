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

    /// <summary>
    /// The "root components are not defined in customizations" warnings from <see cref="Warnings"/>.
    /// After Pack, entity forms that were packed are already removed from them, since the packager reports those as missing too.
    /// </summary>
    public IReadOnlyList<string> MissingRootComponentWarnings { get; }

    /// <summary>
    /// The components listed in <see cref="MissingRootComponentWarnings"/>.
    /// A warning with a row in an unexpected format adds nothing here, so use <see cref="HasMissingRootComponents"/> to fail a build.
    /// </summary>
    public IReadOnlyList<MissingRootComponent> MissingRootComponents { get; }

    public bool HasErrors => Errors.Count > 0;

    public bool HasMissingRootComponents => MissingRootComponentWarnings.Count > 0;

    internal SolutionPackagerResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
        MissingRootComponentWarnings = warnings
            .Where(IsMissingRootComponentsWarning)
            .ToArray();
        MissingRootComponents = MissingRootComponentWarnings
            .SelectMany(warning => MissingRootComponent.TryParseWarning(warning, out var components) ? components : [])
            .ToArray();
    }

    internal static bool IsMissingRootComponentsWarning(string warning)
    {
        return warning.StartsWith(MissingRootComponentsWarningPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
