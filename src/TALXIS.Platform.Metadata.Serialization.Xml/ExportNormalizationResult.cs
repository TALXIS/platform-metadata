namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Normalization rule that produced an <see cref="ExportNormalizationChange"/>.
/// </summary>
public enum ExportNormalizationRule
{
    /// <summary>
    /// A server-added system relationship not present in the source project was removed.
    /// </summary>
    SystemRelationship,

    /// <summary>
    /// A component neither listed as a source root component nor present in the source project was removed.
    /// </summary>
    ComponentNotInSource,

    /// <summary>
    /// A subcomponent owned by a different source solution was removed.
    /// </summary>
    CrossSolutionComponent,

    /// <summary>
    /// A subcomponent was removed because its entity is declared with a behavior
    /// that excludes subcomponents in the source solution.
    /// </summary>
    ExcludedSubcomponent,

    /// <summary>
    /// A server-enriched attribute was removed from a roundtrip document.
    /// </summary>
    ServerVersionAttribute,

    /// <summary>
    /// A server-owned entity attribute absent from the source entity was removed.
    /// </summary>
    ServerOwnedAttribute,

    /// <summary>
    /// The Managed flag was aligned with the source solution.
    /// </summary>
    ManagedFlag,

    /// <summary>
    /// The solution version was aligned with the source solution.
    /// </summary>
    SolutionVersion
}

/// <summary>
/// A single modification applied by <see cref="ExportNormalizer"/>.
/// </summary>
public sealed class ExportNormalizationChange
{
    /// <summary>
    /// Creates a normalization change record.
    /// </summary>
    public ExportNormalizationChange(ExportNormalizationRule rule, string target, string description, ComponentType? componentType = null)
    {
        Rule = rule;
        Target = target;
        Description = description;
        ComponentType = componentType;
    }

    /// <summary>
    /// Gets the rule that produced this change.
    /// </summary>
    public ExportNormalizationRule Rule { get; }

    /// <summary>
    /// Gets the affected component, attribute, or property identity.
    /// For removed components this is the identity key accepted by the <see cref="Workspace"/> Remove methods.
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets the component type for removed components; null for manifest and attribute changes.
    /// </summary>
    public ComponentType? ComponentType { get; }

    /// <summary>
    /// Gets a human-readable description of the change.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Returns "<c>Rule: Description</c>" for logging.
    /// </summary>
    public override string ToString() => $"{Rule}: {Description}";
}

/// <summary>
/// Report of all modifications applied by a normalization run.
/// </summary>
public sealed class ExportNormalizationResult
{
    /// <summary>
    /// Creates a normalization result.
    /// </summary>
    public ExportNormalizationResult(IReadOnlyList<ExportNormalizationChange> changes)
    {
        Changes = changes ?? throw new ArgumentNullException(nameof(changes));
    }

    /// <summary>
    /// Gets the applied changes in application order.
    /// </summary>
    public IReadOnlyList<ExportNormalizationChange> Changes { get; }

    /// <summary>
    /// Gets whether the normalization modified the workspace.
    /// </summary>
    public bool HasChanges => Changes.Count > 0;
}
