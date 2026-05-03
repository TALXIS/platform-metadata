namespace TALXIS.Platform.Metadata;

/// <summary>
/// Defines a component type's behavior — how it serializes, where it lives on disk,
/// how its identity is determined, and how it participates in solution layering.
/// Data-driven replacement for per-type processor classes.
/// Behavioral properties sourced from the decompiled <c>IComponentDefinition</c> interface
/// in <c>Microsoft.Crm.Caching</c>.
/// </summary>
public sealed record ComponentDefinition(
    ComponentType TypeCode,
    string Name,
    string SerializedName,
    string Directory,
    string FilePattern,
    IdentityStrategy Identity,
    bool SupportsMerge = false,
    bool IsFileBacked = false,
    bool HasSubfolders = false,

    // ── Behavioral flags (from IComponentDefinition) ──

    /// <summary>Whether the component uses XML merge (forms, sitemaps) vs top-wins replacement.</summary>
    bool IsMergeable = false,
    /// <summary>Whether this is a child component (e.g., attribute is child of entity).</summary>
    bool HasParent = false,
    /// <summary>Parent component type code. 0 means the component is a root component.</summary>
    int RootComponent = 0,
    /// <summary>Whether customizations can be overwritten on import.</summary>
    bool AllowOverwriteCustomizations = false,
    /// <summary>Whether force-update is always applied during import.</summary>
    bool UseForceUpdateAlways = false,
    /// <summary>Whether the component supports IsCustomizable managed property.</summary>
    bool IsCustomizable = false,
    /// <summary>Whether the component can be deleted from a solution.</summary>
    bool CanBeDeleted = false,

    // ── Identity / serialization (from IComponentDefinition) ──

    /// <summary>Format-specific selector path used to locate this component in serialized manifests.</summary>
    string? SerializedPath = null,
    /// <summary>Comma-separated attribute names that form the export identity key.</summary>
    string? ExportKeyAttributes = null,
    /// <summary>Whether the component has a composite export key.</summary>
    bool HasExportKey = false,
    /// <summary>Primary key attribute name on the backing entity.</summary>
    string? PrimaryKeyName = null,
    /// <summary>Component type of the group parent (0 = none).</summary>
    int? GroupParentComponentType = null,
    /// <summary>Foreign key attribute name pointing to the group parent.</summary>
    string? GroupParentComponentAttributeName = null
);
