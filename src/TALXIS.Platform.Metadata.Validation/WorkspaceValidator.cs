using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Unified validation entry point. Runs all registered validators
/// and optionally loads the workspace into the typed model.
/// Consumers call this single method instead of wiring validators individually.
/// </summary>
public sealed class WorkspaceValidator
{
    /// <summary>
    /// Runs all validation checks on a solution workspace directory:
    /// XSD schema validation, JSON schema validation, duplicate GUID detection,
    /// and model loading (reports parse errors and component counts).
    /// </summary>
    public WorkspaceValidationReport ValidateDirectory(string workspacePath)
    {
        var results = new List<ValidationResult>();

        if (!Directory.Exists(workspacePath))
        {
            results.Add(new ValidationResult(ValidationSeverity.Error,
                $"Directory not found: {workspacePath}", null, null, null));
            return new WorkspaceValidationReport(results, null);
        }

        // Layer 1: Schema validation
        var schemaValidator = new SchemaValidator();
        foreach (var xmlFile in Directory.EnumerateFiles(workspacePath, "*.xml", SearchOption.AllDirectories))
        {
            results.AddRange(schemaValidator.ValidateFile(xmlFile));
        }

        // Layer 1: JSON validation
        var jsonValidator = new JsonValidator();
        foreach (var jsonFile in Directory.EnumerateFiles(workspacePath, "*.json", SearchOption.AllDirectories))
        {
            results.AddRange(jsonValidator.ValidateFile(jsonFile));
        }

        // Layer 1: GUID duplicate detection
        var guidValidator = new GuidValidator();
        results.AddRange(guidValidator.ValidateDirectory(workspacePath));

        // Layer 2: Model loading
        Workspace? workspace = null;
        try
        {
            var reader = new XmlWorkspaceReader();
            workspace = reader.Load(workspacePath);

            // Report load errors as validation warnings
            foreach (var loadError in workspace.LoadErrors)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    $"Load error: {loadError.Message}",
                    loadError.FilePath, null, null));
            }
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Failed to load workspace into model: {ex.Message}",
                workspacePath, null, null));
        }

        return new WorkspaceValidationReport(results, workspace);
    }
}

/// <summary>
/// Results from a full workspace validation run.
/// </summary>
public sealed class WorkspaceValidationReport
{
    public IReadOnlyList<ValidationResult> Results { get; }
    public Workspace? Workspace { get; }

    public int ErrorCount => Results.Count(r => r.Severity == ValidationSeverity.Error);
    public int WarningCount => Results.Count(r => r.Severity == ValidationSeverity.Warning);

    /// <summary>
    /// Summary of components loaded into the model (null if loading failed).
    /// </summary>
    public ComponentSummary? LoadedComponents { get; }

    public WorkspaceValidationReport(IReadOnlyList<ValidationResult> results, Workspace? workspace)
    {
        Results = results;
        Workspace = workspace;
        if (workspace != null)
        {
            LoadedComponents = new ComponentSummary(workspace);
        }
    }
}

/// <summary>
/// Counts of each component type loaded from the workspace.
/// </summary>
public sealed class ComponentSummary
{
    public int Entities { get; }
    public int Attributes { get; }
    public int Forms { get; }
    public int Views { get; }
    public int GlobalOptionSets { get; }
    public int Relationships { get; }
    public int PluginAssemblies { get; }
    public int SdkMessageProcessingSteps { get; }
    public int SecurityRoles { get; }
    public int AppModules { get; }
    public int SiteMaps { get; }
    public int WebResources { get; }
    public int Workflows { get; }
    public int GenericComponents { get; }
    public int Total => Entities + Forms + Views + GlobalOptionSets + Relationships +
                        PluginAssemblies + SdkMessageProcessingSteps + SecurityRoles +
                        AppModules + SiteMaps + WebResources + Workflows + GenericComponents;

    public ComponentSummary(Workspace workspace)
    {
        Entities = workspace.Entities.Count;
        Attributes = workspace.Entities.Sum(e => e.Attributes.Count);
        Forms = workspace.Forms.Count;
        Views = workspace.Views.Count;
        GlobalOptionSets = workspace.GlobalOptionSets.Count;
        Relationships = workspace.Relationships.Count;
        PluginAssemblies = workspace.PluginAssemblies.Count;
        SdkMessageProcessingSteps = workspace.SdkMessageProcessingSteps.Count;
        SecurityRoles = workspace.SecurityRoles.Count;
        AppModules = workspace.AppModules.Count;
        SiteMaps = workspace.SiteMaps.Count;
        WebResources = workspace.WebResources.Count;
        Workflows = workspace.Workflows.Count;
        GenericComponents = workspace.GenericComponents.Count;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Entities > 0) parts.Add($"{Entities} entities ({Attributes} attributes)");
        if (Forms > 0) parts.Add($"{Forms} forms");
        if (Views > 0) parts.Add($"{Views} views");
        if (GlobalOptionSets > 0) parts.Add($"{GlobalOptionSets} option sets");
        if (Relationships > 0) parts.Add($"{Relationships} relationships");
        if (PluginAssemblies > 0) parts.Add($"{PluginAssemblies} plugin assemblies");
        if (SdkMessageProcessingSteps > 0) parts.Add($"{SdkMessageProcessingSteps} steps");
        if (SecurityRoles > 0) parts.Add($"{SecurityRoles} roles");
        if (AppModules > 0) parts.Add($"{AppModules} app modules");
        if (SiteMaps > 0) parts.Add($"{SiteMaps} sitemaps");
        if (WebResources > 0) parts.Add($"{WebResources} web resources");
        if (Workflows > 0) parts.Add($"{Workflows} workflows");
        if (GenericComponents > 0) parts.Add($"{GenericComponents} generic");
        return string.Join(", ", parts);
    }
}
