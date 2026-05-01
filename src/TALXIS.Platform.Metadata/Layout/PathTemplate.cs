namespace TALXIS.Platform.Metadata.Layout;

/// <summary>
/// Placeholder names used in <see cref="ComponentDefinition"/> file patterns.
/// Resolved at runtime by the path resolver.
/// </summary>
public static class PathTemplate
{
    /// <summary>Component's primary name, sanitized for file system use.</summary>
    public const string PrimaryName = "$(PrimaryName)";

    /// <summary>ComponentType.ToString() (e.g. "SiteMap", "Report").</summary>
    public const string Type = "$(type)";

    /// <summary>"_managed" for managed solutions, empty string otherwise.</summary>
    public const string Managed = "$(managed)";

    /// <summary>XML element name — used for GenericComponent and Connector dynamic directories.</summary>
    public const string ComponentsRootName = "$(ComponentsRootName)";
}
