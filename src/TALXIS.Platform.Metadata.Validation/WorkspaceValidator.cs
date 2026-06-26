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

        // Layer 1: XSD schema validation, one file at a time.
        ValidateFiles(workspacePath, "*.xml", ValidationStage.Schema, new SchemaValidator().ValidateFile, results);

        // Layer 2: JSON schema validation, one file at a time.
        ValidateFiles(workspacePath, "*.json", ValidationStage.Json, new JsonValidator().ValidateFile, results);

        // Layer 3: duplicate GUID detection across the whole tree.
        results.AddRange(WithStage(new GuidValidator().ValidateDirectory(workspacePath), ValidationStage.DuplicateGuid));

        // Layer 4: load each solution into the model and validate it (load errors, flows, relationships).
        var workspace = ValidateModel(workspacePath, results);

        return BuildReport(results, workspace);
    }

    private static void ValidateFiles(
        string workspacePath,
        string pattern,
        ValidationStage stage,
        Func<string, IReadOnlyList<ValidationResult>> validate,
        List<ValidationResult> results)
    {
        foreach (var file in EnumerateWorkspaceFiles(workspacePath, pattern))
        {
            try
            {
                results.AddRange(WithStage(validate(file), stage));
            }
            catch (IOException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Cannot read file: {ex.Message}", file, null, null) { Stage = stage });
            }
            catch (UnauthorizedAccessException ex)
            {
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    $"Access denied: {ex.Message}", file, null, null) { Stage = stage });
            }
        }
    }

    private static Workspace? ValidateModel(string workspacePath, List<ValidationResult> results)
    {
        try
        {
            var reader = new XmlWorkspaceReader();
            var solutionRoots = DiscoverSolutionRoots(workspacePath);
            if (solutionRoots.Count == 0)
                solutionRoots = new[] { workspacePath };

            var loaded = solutionRoots.Select(reader.Load).ToList();
            var workspaceColumns = BuildWorkspaceColumnSet(loaded);
            foreach (var ws in loaded)
                CollectModelFindings(ws, workspaceColumns, results);

            return loaded.Count == 1
                ? loaded[0]
                : reader.LoadMany(solutionRoots.Select((path, index) => new SolutionWorkspaceSource(path, index)));
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Failed to load workspace into model: {ex.Message}",
                workspacePath, null, null) { Stage = ValidationStage.ModelLoad });
            return null;
        }
    }



    private static HashSet<string> BuildWorkspaceColumnSet(IEnumerable<Workspace> workspaces)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workspace in workspaces)
            foreach (var entity in workspace.Entities)
                foreach (var attribute in entity.Attributes)
                    columns.Add($"{entity.LogicalName}|{attribute.LogicalName}");
        return columns;
    }

    private static void CollectModelFindings(Workspace workspace, HashSet<string> workspaceColumns, List<ValidationResult> results)
    {
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

        results.AddRange(WithStage(new RelationshipValidator().Validate(workspace, workspaceColumns), ValidationStage.Relationship));
    }

    private static WorkspaceValidationReport BuildReport(IEnumerable<ValidationResult> results, Workspace? workspace)
    {
        var labeled = results
            .Select(r => r with { Message = $"[{r.Stage.Label()}] {r.Message}" })
            .ToList();
        return new WorkspaceValidationReport(labeled, workspace);
    }

    private static IReadOnlyList<string> DiscoverSolutionRoots(string workspacePath)
    {
        if (File.Exists(Path.Combine(workspacePath, "Other", "Solution.xml")))
            return System.Array.Empty<string>();

        var roots = new List<string>();
        var pending = new Stack<string>();
        pending.Push(workspacePath);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(dir))
            {
                if (IgnoredDirectories.Contains(Path.GetFileName(child)))
                    continue;

                if (File.Exists(Path.Combine(child, "Other", "Solution.xml")))
                    roots.Add(child);
                else
                    pending.Push(child);
            }
        }

        return roots;
    }

    private static IEnumerable<ValidationResult> WithStage(IEnumerable<ValidationResult> results, ValidationStage stage) => results.Select(r => r with { Stage = stage });
    private static ValidationSeverity MapFlowSeverity(FlowDiagnosticSeverity severity) => severity == FlowDiagnosticSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning;
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
