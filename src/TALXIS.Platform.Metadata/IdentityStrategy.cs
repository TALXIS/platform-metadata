namespace TALXIS.Platform.Metadata;

/// <summary>
/// How a component's identity is determined for deduplication and file naming.
/// </summary>
public enum IdentityStrategy
{
    /// <summary>Identity by GUID (e.g., Workflow, WebResource, Role).</summary>
    Guid,

    /// <summary>Identity by logical/schema name (e.g., Entity, OptionSet).</summary>
    Name,

    /// <summary>Identity by composite key (e.g., EntityMap → "Source,Target").</summary>
    Composite,

    /// <summary>Singleton component — only one per solution (e.g., SiteMap, RibbonCustomization).</summary>
    Singleton,

    /// <summary>Identity probed dynamically (GenericComponent).</summary>
    Probed
}
