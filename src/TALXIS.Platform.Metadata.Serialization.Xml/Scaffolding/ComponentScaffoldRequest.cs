namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Component-agnostic scaffold payload: rendered template files and scalar parameters
/// keyed by well-known role names. The component type selects the applier.
/// </summary>
public sealed class ComponentScaffoldRequest
{
    /// <summary>
    /// Component type matching the template's componentType tag (e.g. "Attribute").
    /// </summary>
    public string ComponentType { get; set; } = "";

    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Rendered file paths keyed by role (e.g. "attribute", "relationship").
    /// </summary>
    public IReadOnlyDictionary<string, string> Files { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Scalar parameters keyed by name (e.g. "entity", "options").
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}
