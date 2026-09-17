namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="EntityFormScaffold"/>: the rendered form file to
/// finalize (id, file name, dialog unique name) and register.
/// </summary>
public sealed class EntityFormScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Form type: "main", "quickCreate" or "dialog".
    /// </summary>
    public string FormType { get; set; } = "";

    /// <summary>
    /// Path to the rendered form file as the template emitted it.
    /// </summary>
    public string FormFilePath { get; set; } = "";

    /// <summary>
    /// GUID for the form, without braces; null generates a new one.
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// Display name of the form; feeds the generated dialog unique name.
    /// </summary>
    public string? FormName { get; set; }

    /// <summary>
    /// Explicit dialog unique name; null derives it from the entity prefix and form name.
    /// </summary>
    public string? DialogUniqueName { get; set; }

    /// <summary>
    /// Schema name of the entity that owns the form (prefix source for dialogs).
    /// </summary>
    public string? EntitySchemaName { get; set; }
}
