namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Stable diagnostic codes for validation rules. Consumers use these to track or suppress an
/// individual rule instead of a whole validation stage, so a code is never reused or renumbered
/// once published.
/// </summary>
public static class ValidationDiagnostics
{
    /// <summary>Finding produced by a rule that has not been assigned a code yet.</summary>
    public const string Unclassified = "TXM000";

    /// <summary>Solution.xml carries no MissingDependencies element, which fails a fresh-environment import.</summary>
    public const string MissingDependenciesElementAbsent = "TXM001";

    /// <summary>A root component declared in Solution.xml has no matching component file in the solution.</summary>
    public const string RootComponentFileAbsent = "TXM002";

    /// <summary>A component exists in the solution source but is not declared as a root component.</summary>
    public const string ComponentNotDeclaredAsRootComponent = "TXM003";

    /// <summary>An SDK message processing step references a plugin assembly that is not part of the solution.</summary>
    public const string StepAssemblyNotInSolution = "TXM004";
}
