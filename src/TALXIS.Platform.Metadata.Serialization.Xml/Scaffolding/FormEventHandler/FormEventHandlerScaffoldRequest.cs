namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Input for <see cref="FormEventHandlerScaffold"/>: the form to patch, the
/// script library to register and the event handler to attach.
/// </summary>
public sealed class FormEventHandlerScaffoldRequest
{
    /// <summary>
    /// Folder containing the unpacked solution files (Other/, Entities/, OptionSets/).
    /// </summary>
    public string SolutionRootPath { get; set; } = "";

    /// <summary>
    /// Logical name of the entity that owns the form; null lets the locator search.
    /// </summary>
    public string? EntityLogicalName { get; set; }

    /// <summary>
    /// Form type ("main", "quick", "Dialog"); null lets the locator search.
    /// </summary>
    public string? FormType { get; set; }

    /// <summary>
    /// GUID of the form to patch, without braces; null lets the locator search.
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// Web resource file name including extension (e.g. "udpp_forms.js").
    /// </summary>
    public string LibraryName { get; set; } = "";

    /// <summary>
    /// GUID for the Library element, without braces; null generates one.
    /// </summary>
    public string? LibraryUniqueId { get; set; }

    /// <summary>
    /// Event name ("onload", "onsave", "onchange", "onclick").
    /// </summary>
    public string EventName { get; set; } = "";

    /// <summary>
    /// Attribute the handler reacts to; used by onchange events only.
    /// </summary>
    public string? AttributeName { get; set; }

    /// <summary>
    /// JavaScript function the handler invokes.
    /// </summary>
    public string FunctionName { get; set; } = "";

    /// <summary>
    /// GUID for the Handler element, without braces; null generates one.
    /// </summary>
    public string? HandlerUniqueId { get; set; }

    /// <summary>
    /// Value of the passExecutionContext attribute ("true"/"false").
    /// </summary>
    public string PassExecutionContext { get; set; } = "true";
}
