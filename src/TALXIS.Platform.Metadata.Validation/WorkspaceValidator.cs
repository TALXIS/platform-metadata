using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Unified validation entry point. Runs all registered validators
/// and optionally loads the workspace into the typed model.
/// Consumers call this single method instead of wiring validators individually.
/// </summary>
public sealed class WorkspaceValidator
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".github", "node_modules", "packages", "TestResults"
    };

    private static IEnumerable<string> EnumerateWorkspaceFiles(string directory, string pattern)
    {
        var fullDir = Path.GetFullPath(directory);
        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
        {
            // Check if any parent directory is in the ignore list
            var fullFile = Path.GetFullPath(file);
            var relativePath = fullFile.Substring(fullDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (parts.Any(p => IgnoredDirectories.Contains(p)))
                continue;
            yield return file;
        }
    }

    /// <summary>
    /// Runs all validation checks on a solution workspace directory:
    /// XSD schema validation, JSON schema validation, duplicate GUID detection,
    /// and model loading (reports parse errors and component counts).
    /// </summary>
    /// <param name="workspacePath">Path to the unpacked SolutionPackager workspace.</param>
    /// <returns>A validation report containing all findings and the loaded workspace when loading succeeded.</returns>
    public WorkspaceValidationReport ValidateDirectory(string workspacePath)
    {
        var results = new List<ValidationResult>();

        if (!Directory.Exists(workspacePath))
        {
            results.Add(new ValidationResult(ValidationSeverity.Error,
                $"Directory not found: {workspacePath}", null, null, null) { Stage = ValidationStage.Workspace });
            return BuildReport(results, null);
        }

        // Layer 1: Schema validation
        var schemaValidator = new SchemaValidator();
        foreach (var xmlFile in EnumerateWorkspaceFiles(workspacePath, "*.xml"))
        {
            try
            {
                results.AddRange(WithStage(schemaValidator.ValidateFile(xmlFile), ValidationStage.Schema));
            }
            catch (IOException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Cannot read file: {ex.Message}", xmlFile, null, null) { Stage = ValidationStage.Schema });
            }
            catch (UnauthorizedAccessException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Access denied: {ex.Message}", xmlFile, null, null) { Stage = ValidationStage.Schema });
            }
        }

        // Layer 1: JSON validation
        var jsonValidator = new JsonValidator();
        foreach (var jsonFile in EnumerateWorkspaceFiles(workspacePath, "*.json"))
        {
            try
            {
                results.AddRange(WithStage(jsonValidator.ValidateFile(jsonFile), ValidationStage.Json));
            }
            catch (IOException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Cannot read file: {ex.Message}", jsonFile, null, null) { Stage = ValidationStage.Json });
            }
            catch (UnauthorizedAccessException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Access denied: {ex.Message}", jsonFile, null, null) { Stage = ValidationStage.Json });
            }
        }

        // Layer 1: GUID duplicate detection
        var guidValidator = new GuidValidator();
        results.AddRange(WithStage(guidValidator.ValidateDirectory(workspacePath), ValidationStage.DuplicateGuid));

        // Layer 2: Model loading
        Workspace? workspace = null;
        try
        {
            var reader = new XmlWorkspaceReader();
            workspace = reader.Load(workspacePath);

            // Report load errors as validation errors (malformed XML is not recoverable)
            foreach (var loadError in workspace.LoadErrors)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Load error: {loadError.Message}",
                    loadError.FilePath,
                    loadError.Line,
                    loadError.Column) { Stage = ValidationStage.ModelLoad });
            }

            foreach (var diagnostic in workspace.FlowDefinitions.SelectMany(f => f.Diagnostics))
            {
                results.Add(new ValidationResult(
                    MapFlowSeverity(diagnostic.Severity),
                    $"Flow {diagnostic.Code}: {diagnostic.Message}",
                    diagnostic.FilePath,
                    diagnostic.Line,
                    diagnostic.Column) { Stage = ValidationStage.Flow });
            }

            // Layer 3: Referential integrity across the loaded model.
            var relationshipValidator = new RelationshipValidator();
            results.AddRange(WithStage(relationshipValidator.Validate(workspace), ValidationStage.Relationship));
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Failed to load workspace into model: {ex.Message}",
                workspacePath, null, null) { Stage = ValidationStage.ModelLoad });
        }

        return BuildReport(results, workspace);
    }

    private static IEnumerable<ValidationResult> WithStage(IEnumerable<ValidationResult> results, ValidationStage stage) =>
        results.Select(r => r with { Stage = stage });

    private static WorkspaceValidationReport BuildReport(IEnumerable<ValidationResult> results, Workspace? workspace)
    {
        var labeled = results
            .Select(r => r with { Message = $"[{r.Stage.Label()}] {r.Message}" })
            .ToList();
        return new WorkspaceValidationReport(labeled, workspace);
    }

    private static ValidationSeverity MapFlowSeverity(FlowDiagnosticSeverity severity) =>
        severity == FlowDiagnosticSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning;
}

/// <summary>
/// Results from a full workspace validation run.
/// </summary>
public sealed class WorkspaceValidationReport
{
    /// <summary>
    /// Gets the individual validation findings.
    /// </summary>
    public IReadOnlyList<ValidationResult> Results { get; }

    /// <summary>
    /// Gets the loaded workspace when model loading succeeded; otherwise <see langword="null"/>.
    /// </summary>
    public Workspace? Workspace { get; }

    /// <summary>
    /// Gets the number of error-level findings.
    /// </summary>
    public int ErrorCount => Results.Count(r => r.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Gets the number of warning-level findings.
    /// </summary>
    public int WarningCount => Results.Count(r => r.Severity == ValidationSeverity.Warning);

    /// <summary>
    /// Summary of components loaded into the model (null if loading failed).
    /// </summary>
    public ComponentSummary? LoadedComponents { get; }

    /// <summary>
    /// Creates a validation report.
    /// </summary>
    /// <param name="results">Validation results produced by the run.</param>
    /// <param name="workspace">Loaded workspace, or <see langword="null"/> when model loading failed.</param>
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
    /// <summary>
    /// Gets the number of solution manifests loaded into the workspace.
    /// </summary>
    public int Solutions { get; }

    /// <summary>
    /// Gets the number of solution/component membership rows loaded into the workspace.
    /// </summary>
    public int SolutionComponentMemberships { get; }

    /// <summary>
    /// Gets the number of source-owned component snapshots loaded into the workspace.
    /// </summary>
    public int ComponentSourceSnapshots { get; }

    /// <summary>
    /// Gets the number of entities loaded into the workspace.
    /// </summary>
    public int Entities { get; }

    /// <summary>
    /// Gets the total number of attributes across all entities.
    /// </summary>
    public int Attributes { get; }

    /// <summary>
    /// Gets the number of forms.
    /// </summary>
    public int Forms { get; }

    /// <summary>
    /// Gets the number of views.
    /// </summary>
    public int Views { get; }

    /// <summary>
    /// Gets the number of global option sets.
    /// </summary>
    public int GlobalOptionSets { get; }

    /// <summary>
    /// Gets the number of relationships.
    /// </summary>
    public int Relationships { get; }

    /// <summary>
    /// Gets the number of plugin assemblies.
    /// </summary>
    public int PluginAssemblies { get; }

    /// <summary>
    /// Gets the number of SDK message processing steps.
    /// </summary>
    public int SdkMessageProcessingSteps { get; }

    /// <summary>
    /// Gets the number of security roles.
    /// </summary>
    public int SecurityRoles { get; }

    /// <summary>
    /// Gets the number of app modules.
    /// </summary>
    public int AppModules { get; }

    /// <summary>
    /// Gets the number of site maps.
    /// </summary>
    public int SiteMaps { get; }

    /// <summary>
    /// Gets the number of web resources.
    /// </summary>
    public int WebResources { get; }

    /// <summary>
    /// Gets the number of workflows.
    /// </summary>
    public int Workflows { get; }

    /// <summary>
    /// Gets the number of parsed flow definitions.
    /// </summary>
    public int FlowDefinitions { get; }

    /// <summary>
    /// Gets the number of generic components.
    /// </summary>
    public int GenericComponents { get; }

    /// <summary>
    /// Gets the total number of top-level loaded component records.
    /// </summary>
    public int Total => Entities + Forms + Views + GlobalOptionSets + Relationships +
                         PluginAssemblies + SdkMessageProcessingSteps + SecurityRoles +
                         AppModules + SiteMaps + WebResources + Workflows + FlowDefinitions + GenericComponents;

    /// <summary>
    /// Creates a component summary from a loaded workspace.
    /// </summary>
    /// <param name="workspace">Workspace to summarize.</param>
    public ComponentSummary(Workspace workspace)
    {
        Entities = workspace.Entities.Count;
        Solutions = workspace.Solutions.Count;
        SolutionComponentMemberships = workspace.SolutionComponentMemberships.Count;
        ComponentSourceSnapshots = workspace.ComponentSourceSnapshots.Count;
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
        FlowDefinitions = workspace.FlowDefinitions.Count;
        GenericComponents = workspace.GenericComponents.Count;
    }

    /// <summary>
    /// Returns a concise human-readable summary suitable for logs or CLI output.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Solutions > 0) parts.Add($"{Solutions} solutions");
        if (SolutionComponentMemberships > 0) parts.Add($"{SolutionComponentMemberships} solution component memberships");
        if (ComponentSourceSnapshots > 0) parts.Add($"{ComponentSourceSnapshots} component source snapshots");
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
        if (FlowDefinitions > 0) parts.Add($"{FlowDefinitions} flow definitions");
        if (GenericComponents > 0) parts.Add($"{GenericComponents} generic");
        return string.Join(", ", parts);
    }
}
