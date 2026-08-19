using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Unified validation entry point for a whole workspace. Discovers every solution root,
/// runs <see cref="SolutionValidator"/> on each, then adds the checks that only make sense
/// across solutions: cross-solution duplicate GUIDs, files outside any solution root, and
/// the combined workspace model.
/// </summary>
public sealed class WorkspaceValidator
{
    /// <summary>
    /// Runs all validation checks on a solution workspace directory:
    /// per-solution checks (XSD, JSON, GUIDs, model rules) plus cross-solution checks.
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

        var solutionRoots = DiscoverSolutionRoots(workspacePath);
        var workspaceIsSingleRoot = solutionRoots.Count == 0;
        if (workspaceIsSingleRoot)
            solutionRoots = new[] { workspacePath };

        var solutionValidator = new SolutionValidator();

        // File-level checks per solution root, then files outside any root, so every file is
        // visited exactly once. Duplicate GUID detection stays workspace-wide: its component
        // identity rules already tell cross-solution layering apart from real duplicates.
        foreach (var root in solutionRoots)
            solutionValidator.CollectSchemaAndJsonFindings(root, results);

        if (!workspaceIsSingleRoot)
        {
            // Enumerated files and discovered roots share the workspacePath base, so ordinal
            // prefix comparison is exact on every platform.
            var rootPrefixes = solutionRoots
                .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
                .ToArray();

            solutionValidator.CollectSchemaAndJsonFindings(workspacePath, results,
                file =>
                {
                    var fullFile = Path.GetFullPath(file);
                    return !rootPrefixes.Any(prefix => fullFile.StartsWith(prefix, StringComparison.Ordinal));
                });
        }

        results.AddRange(SolutionValidator.WithStage(
            new GuidValidator().ValidateDirectory(workspacePath), ValidationStage.DuplicateGuid));

        var loaded = new List<(string Root, Workspace? Workspace)>();
        foreach (var root in solutionRoots)
            loaded.Add((root, SolutionValidator.TryLoad(root, results)));

        foreach (var (root, solution) in loaded)
        {
            if (solution != null)
                solutionValidator.CollectModelFindingsSafe(solution, results, root);
        }

        CollectRelationshipFindings(loaded, results);

        var workspace = BuildCombinedWorkspace(workspacePath, solutionRoots, loaded, results);
        return BuildReport(results, workspace);
    }

    /// <summary>
    /// Runs only the workspace-scoped relationship rules: every solution's relationships are
    /// checked against the entities and columns of the whole workspace, since a relationship and
    /// the entity it references may ship in different solutions. Meant for a single pre-build
    /// pass over a multi-solution workspace; per-solution rules stay in <see cref="SolutionValidator"/>.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root containing one or more unpacked solutions.</param>
    public WorkspaceValidationReport ValidateRelationships(string workspacePath)
    {
        var results = new List<ValidationResult>();

        if (!Directory.Exists(workspacePath))
        {
            results.Add(new ValidationResult(ValidationSeverity.Error,
                $"Directory not found: {workspacePath}", null, null, null) { Stage = ValidationStage.Workspace });

            return BuildReport(results, null);
        }

        var solutionRoots = DiscoverSolutionRoots(workspacePath);
        if (solutionRoots.Count == 0)
            solutionRoots = new[] { workspacePath };

        var loaded = new List<(string Root, Workspace? Workspace)>();
        foreach (var root in solutionRoots)
            loaded.Add((root, SolutionValidator.TryLoad(root, results)));

        CollectRelationshipFindings(loaded, results);

        return BuildReport(results, null);
    }

    private static void CollectRelationshipFindings(List<(string Root, Workspace? Workspace)> loaded, List<ValidationResult> results)
    {
        var workspaceColumns = BuildWorkspaceColumnSet(loaded.Select(l => l.Workspace).OfType<Workspace>());

        foreach (var (root, solution) in loaded)
        {
            if (solution == null) continue;

            try
            {
                results.AddRange(SolutionValidator.WithStage(
                    new RelationshipValidator().Validate(solution, workspaceColumns), ValidationStage.Relationship));
            }
            catch (Exception ex)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Failed to load workspace into model: {ex.Message}",
                    root, null, null) { Stage = ValidationStage.ModelLoad });
            }
        }
    }

    private static Workspace? BuildCombinedWorkspace(
        string workspacePath,
        IReadOnlyList<string> solutionRoots,
        List<(string Root, Workspace? Workspace)> loaded,
        List<ValidationResult> results)
    {
        if (loaded.Any(l => l.Workspace == null)) return null;
        if (loaded.Count == 1) return loaded[0].Workspace;

        try
        {
            return new XmlWorkspaceReader().LoadMany(
                solutionRoots.Select((path, index) => new SolutionWorkspaceSource(path, index)));
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

    internal static WorkspaceValidationReport BuildReport(IEnumerable<ValidationResult> results, Workspace? workspace)
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
                if (WorkspaceFiles.IgnoredDirectories.Contains(Path.GetFileName(child)))
                    continue;

                if (File.Exists(Path.Combine(child, "Other", "Solution.xml")))
                    roots.Add(child);
                else
                    pending.Push(child);
            }
        }

        return roots;
    }
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
