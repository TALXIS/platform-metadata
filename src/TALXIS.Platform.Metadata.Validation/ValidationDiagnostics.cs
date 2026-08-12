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

    /// <summary>A root component declared in Solution.xml has no matching component file in the solution.</summary>
    public const string RootComponentFileAbsent = "TXM001";

    /// <summary>A component exists in the solution source but is not declared as a root component.</summary>
    public const string ComponentNotDeclaredAsRootComponent = "TXM002";

    /// <summary>An SDK message processing step references a plugin assembly that is not part of this source — it may legitimately ship from another solution.</summary>
    public const string StepAssemblyNotInSolution = "TXM003";

    /// <summary>A solution manifest declares more than one root component of the same type with the same identity (schema name or id).</summary>
    public const string DuplicateRootComponent = "TXM004";
}
