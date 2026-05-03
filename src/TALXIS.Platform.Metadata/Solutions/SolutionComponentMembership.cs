namespace TALXIS.Platform.Metadata.Solutions;

/// <summary>
/// Describes that a solution contains or root-owns a component.
/// This mirrors Dataverse <c>solutioncomponent</c> membership and is distinct from component layers.
/// </summary>
public sealed class SolutionComponentMembership
{
    /// <summary>
    /// Gets or sets the unique name of the solution that contains the component.
    /// </summary>
    public required string SolutionUniqueName { get; set; }

    /// <summary>
    /// Gets or sets the optional solution identifier when available from a live environment.
    /// </summary>
    public Guid? SolutionId { get; set; }

    /// <summary>
    /// Gets or sets the component identity.
    /// </summary>
    public required ComponentIdentity Identity { get; set; }

    /// <summary>
    /// Gets or sets the root component behavior declared by the solution, when this is a root component.
    /// </summary>
    public RootComponentBehavior? RootComponentBehavior { get; set; }

    /// <summary>
    /// Gets or sets the optional root solution component identifier.
    /// </summary>
    public Guid? RootSolutionComponentId { get; set; }

    /// <summary>
    /// Gets or sets the source project root that supplied this membership.
    /// </summary>
    public string? SourceRootPath { get; set; }

    /// <summary>
    /// Gets or sets the source document key/path that supplied this membership.
    /// </summary>
    public string? SourceDocumentKey { get; set; }
}
