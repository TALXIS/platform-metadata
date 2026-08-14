using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates a single solution root with every rule that needs no cross-solution knowledge:
/// XSD and JSON schema checks, duplicate GUID detection, model load, flow and solution manifest
/// rules. Relationship rules are workspace-scoped (a relationship and the entity it references
/// may ship in different solutions), so they live in <see cref="WorkspaceValidator"/>.
/// <see cref="WorkspaceValidator"/> composes this validator over each solution it discovers;
/// build pipelines call it directly on one solution source folder.
/// </summary>
public sealed class SolutionValidator
{
    private readonly SchemaValidator _schemaValidator;
    private readonly JsonValidator _jsonValidator;

    /// <summary>
    /// Creates a validator with freshly compiled embedded schema sets.
    /// </summary>
    public SolutionValidator() : this(new SchemaValidator(), new JsonValidator()) { }

    internal SolutionValidator(SchemaValidator schemaValidator, JsonValidator jsonValidator)
    {
        _schemaValidator = schemaValidator;
        _jsonValidator = jsonValidator;
    }

    /// <summary>
    /// Runs all single-solution checks on a solution root directory.
    /// </summary>
    /// <param name="solutionRootPath">Directory containing the unpacked solution (the folder with Other/Solution.xml).</param>
    public WorkspaceValidationReport Validate(string solutionRootPath)
    {
        var results = new List<ValidationResult>();

        if (!Directory.Exists(solutionRootPath))
        {
            results.Add(new ValidationResult(ValidationSeverity.Error,
                $"Directory not found: {solutionRootPath}", null, null, null));
            return WorkspaceValidator.BuildReport(results, null);
        }

        if (!File.Exists(Path.Combine(solutionRootPath, "Other", "Solution.xml")))
        {
            results.Add(new ValidationResult(ValidationSeverity.Warning,
                $"No Other/Solution.xml found under '{solutionRootPath}'. If this directory holds multiple solutions, validate it with {nameof(WorkspaceValidator)} instead.",
                null, null, null) { Code = ValidationDiagnostics.SolutionManifestFileAbsent });
        }

        CollectFileFindings(solutionRootPath, results);

        var workspace = TryLoad(solutionRootPath, results);
        if (workspace != null)
            CollectModelFindingsSafe(workspace, results, solutionRootPath);

        return WorkspaceValidator.BuildReport(results, workspace);
    }

    /// <summary>
    /// Runs all single-solution checks against an already-loaded solution workspace.
    /// </summary>
    public WorkspaceValidationReport Validate(Workspace solution, string solutionRootPath)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));

        var results = new List<ValidationResult>();
        if (Directory.Exists(solutionRootPath))
            CollectFileFindings(solutionRootPath, results);

        CollectModelFindingsSafe(solution, results, solutionRootPath);
        return WorkspaceValidator.BuildReport(results, solution);
    }

    /// <summary>
    /// Model checks with the same safety net the load has: a crashing rule becomes a finding,
    /// not an exception escaping to the consumer.
    /// </summary>
    internal void CollectModelFindingsSafe(Workspace workspace, List<ValidationResult> results, string solutionRootPath)
    {
        try
        {
            CollectModelFindings(workspace, results);
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Failed to load workspace into model: {ex.Message}",
                solutionRootPath, null, null) { Stage = ValidationStage.ModelLoad });
        }
    }

    /// <summary>
    /// File-level checks for one solution root: XSD schemas, JSON schemas, duplicate GUIDs.
    /// </summary>
    internal void CollectFileFindings(string solutionRootPath, List<ValidationResult> results)
    {
        CollectSchemaAndJsonFindings(solutionRootPath, results);
        results.AddRange(WithStage(new GuidValidator().ValidateDirectory(solutionRootPath), ValidationStage.DuplicateGuid));
    }

    /// <summary>
    /// XSD and JSON checks only, optionally restricted to files matching <paramref name="includeFile"/>.
    /// </summary>
    internal void CollectSchemaAndJsonFindings(string directory, List<ValidationResult> results, Func<string, bool>? includeFile = null)
    {
        ValidateFiles(directory, "*.xml", ValidationStage.Schema,
            file => WorkspaceFiles.IsWebResourcePayload(file) ? Array.Empty<ValidationResult>() : _schemaValidator.ValidateFile(file),
            results, includeFile);

        ValidateFiles(directory, "*.json", ValidationStage.Json, _jsonValidator.ValidateFile, results, includeFile);
    }

    /// <summary>
    /// Model-level checks for one loaded solution: load errors, flow diagnostics,
    /// solution manifest rules.
    /// </summary>
    private void CollectModelFindings(Workspace workspace, List<ValidationResult> results)
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

        results.AddRange(new SolutionManifestValidator().Validate(workspace));
    }

    internal static Workspace? TryLoad(string solutionRootPath, List<ValidationResult> results)
    {
        try
        {
            return new XmlWorkspaceReader().Load(solutionRootPath);
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Failed to load workspace into model: {ex.Message}",
                solutionRootPath, null, null) { Stage = ValidationStage.ModelLoad });
            return null;
        }
    }

    private static void ValidateFiles(
        string directory,
        string pattern,
        ValidationStage stage,
        Func<string, IReadOnlyList<ValidationResult>> validate,
        List<ValidationResult> results,
        Func<string, bool>? includeFile)
    {
        foreach (var file in WorkspaceFiles.Enumerate(directory, pattern))
        {
            if (includeFile != null && !includeFile(file)) continue;

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

    internal static IEnumerable<ValidationResult> WithStage(IEnumerable<ValidationResult> results, ValidationStage stage) => results.Select(r => r with { Stage = stage });

    private static ValidationSeverity MapFlowSeverity(FlowDiagnosticSeverity severity) => severity == FlowDiagnosticSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning;
}
