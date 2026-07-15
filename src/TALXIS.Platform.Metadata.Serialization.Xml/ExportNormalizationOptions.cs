namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Controls which post-export normalization rules <see cref="ExportNormalizer"/> applies.
/// All rules are enabled by default.
/// </summary>
public sealed class ExportNormalizationOptions
{
    /// <summary>
    /// Gets or sets whether server-added system relationships (BusinessUnit, Owner, SystemUser, Team)
    /// that are not present in the source project are removed.
    /// </summary>
    public bool StripSystemRelationships { get; set; } = true;

    /// <summary>
    /// Gets or sets whether components that are neither listed as root components in the source
    /// Solution.xml nor present in the source project are removed.
    /// Skipped when the source solution declares no root components at all (bootstrap scenario).
    /// </summary>
    public bool StripComponentsNotInSource { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the source root-component behavior is enforced on subcomponents:
    /// forms, views, and ribbons of entities declared with behavior 1 or 2 are removed from the export.
    /// </summary>
    public bool EnforceRootComponentBehavior { get; set; } = true;

    /// <summary>
    /// Gets or sets whether forms and views owned by a different source solution are removed.
    /// This targets cross-solution subcomponent leaks and requires a multi-solution source workspace to have effect.
    /// </summary>
    public bool StripComponentsOwnedByOtherSolutions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether server-enriched root attributes (OrganizationVersion, OrganizationSchemaType,
    /// CRMServerServiceabilityVersion) are removed from Solution.xml and Customizations.xml.
    /// </summary>
    public bool StripServerVersionAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the exported Managed flag is aligned with the source solution.
    /// </summary>
    public bool NormalizeManagedFlag { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the exported solution version is aligned with the source solution.
    /// </summary>
    public bool NormalizeSolutionVersion { get; set; } = true;
}
