namespace TALXIS.Platform.Metadata;

/// <summary>
/// Defines a component type's behavior — how it serializes, where it lives on disk,
/// and how its identity is determined. Data-driven replacement for per-type processor classes.
/// </summary>
public sealed record ComponentDefinition(
    ComponentType TypeCode,
    string Name,
    string XmlElementName,
    string Directory,
    string FilePattern,
    IdentityStrategy Identity,
    bool SupportsMerge = false,
    bool IsFileBacked = false,
    bool HasSubfolders = false
);
