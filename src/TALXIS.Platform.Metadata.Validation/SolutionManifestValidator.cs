using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Validates a solution manifest against the component files that ship with it: every declared
/// root component must exist on disk, every root-level component on disk should be declared, and
/// the manifest must carry the dependency section the platform expects on import.
/// Needs only one solution root, so it is safe to run on every build.
/// </summary>
public sealed class SolutionManifestValidator
{
    /// <summary>
    /// Root component types whose <c>solution.xml</c> identity can be compared directly against the
    /// identity the reader assigns to the loaded component. Types outside this set are skipped
    /// rather than guessed at, so the rule never reports a component it merely failed to match.
    /// </summary>
    private static readonly HashSet<ComponentType> ComparableTypes = new()
    {
        ComponentType.Entity, ComponentType.OptionSet, ComponentType.EntityRelationship,
        ComponentType.SystemForm, ComponentType.SavedQuery, ComponentType.Workflow,
        ComponentType.Role, ComponentType.WebResource, ComponentType.PluginAssembly,
        ComponentType.SdkMessageProcessingStep
    };

    /// <summary>
    /// Types that are always root components. Subcomponents such as forms and views are covered by
    /// their parent entity's root component, so they must not be reported as undeclared.
    /// WebResource and PluginAssembly are excluded: the TALXIS DevKit build declares them
    /// automatically (AddRootComponentToSolution), so source needs no declaration.
    /// </summary>
    private static readonly HashSet<ComponentType> AlwaysRootTypes = new()
    {
        ComponentType.Entity, ComponentType.OptionSet, ComponentType.Workflow,
        ComponentType.Role
    };

    /// <summary>
    /// Validates every solution manifest loaded into the workspace.
    /// </summary>
    /// <param name="workspace">Loaded workspace.</param>
    /// <returns>Findings, ordered per solution.</returns>
    public IReadOnlyList<ValidationResult> Validate(Workspace workspace)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));

        var results = new List<ValidationResult>();
        var loaded = BuildLoadedComponentIndex(workspace);

        // The loaded index is workspace-wide, so "on disk but undeclared" can only be decided when a
        // single manifest owns everything in it. In a multi-solution workspace the component may
        // legitimately belong to a sibling solution.
        var canAttributeComponentsToSolution = workspace.Solutions.Count == 1;

        foreach (var solution in workspace.Solutions)
        {
            ValidateMissingDependencies(solution, results);
            ValidateDeclaredComponentsExist(solution, SupplementFromDisk(solution, loaded), results);

            if (canAttributeComponentsToSolution)
                ValidateComponentsAreDeclared(solution, loaded, results);
        }

        if (canAttributeComponentsToSolution)
            ValidateStepAssemblyReferences(workspace, results);

        return results;
    }

    private static void ValidateStepAssemblyReferences(Workspace workspace, List<ValidationResult> results)
    {
        if (workspace.SdkMessageProcessingSteps.Count == 0) return;

        var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginAssembly in workspace.PluginAssemblies)
        {
            if (pluginAssembly.Name != null) assemblies.Add(Normalize(ComponentType.PluginAssembly, pluginAssembly.Name));
            if (pluginAssembly.FullName != null) assemblies.Add(Normalize(ComponentType.PluginAssembly, pluginAssembly.FullName));
        }

        foreach (var step in workspace.SdkMessageProcessingSteps)
        {
            var assemblyName = GetStepAssemblySimpleName(step.PluginTypeName);
            if (assemblyName == null) continue;
            if (assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)) continue;
            if (assemblies.Contains(assemblyName)) continue;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"SdkMessageProcessingStep '{step.Name ?? step.SdkMessageProcessingStepId}' references plugin assembly " +
                $"'{assemblyName}' that is not part of the solution. The step registration fails on import.",
                step.Source?.FilePath, step.Source?.Line, step.Source?.Column)
            {
                Stage = ValidationStage.SolutionManifest,
                Code = ValidationDiagnostics.StepAssemblyNotInSolution
            });
        }
    }

    private static string? GetStepAssemblySimpleName(string? pluginTypeName)
    {
        if (string.IsNullOrWhiteSpace(pluginTypeName)) return null;

        var parts = pluginTypeName!.Split(',');
        if (parts.Length < 2) return null;

        var assembly = parts[1].Trim();
        return assembly.Length == 0 || assembly.Contains('=') ? null : Normalize(ComponentType.PluginAssembly, assembly);
    }

    private static Dictionary<ComponentType, HashSet<string>> BuildLoadedComponentIndex(Workspace workspace)
    {
        var index = new Dictionary<ComponentType, HashSet<string>>();
        foreach (var component in workspace.EnumerateLayerComponents())
        {
            if (string.IsNullOrWhiteSpace(component.ObjectId)) continue;
            Add(index, component.Type, component.ObjectId);
        }

        // Manifests declare these types by schema name while the descriptors carry ids,
        // so both identities must be present in the index.
        foreach (var webResource in workspace.WebResources)
            Add(index, ComponentType.WebResource, webResource.Name);
        foreach (var pluginAssembly in workspace.PluginAssemblies)
        {
            Add(index, ComponentType.PluginAssembly, pluginAssembly.Name);
            Add(index, ComponentType.PluginAssembly, pluginAssembly.FullName);
        }

        return index;
    }

    /// <summary>
    /// Extends the loaded index with components the reader does not (yet) load into the model:
    /// stub Entity.xml files without EntityInfo, dashboards, and dialogs.
    /// </summary>
    private static Dictionary<ComponentType, HashSet<string>> SupplementFromDisk(
        Solution solution,
        Dictionary<ComponentType, HashSet<string>> loaded)
    {
        var solutionFile = solution.Source?.FilePath;
        var otherDir = solutionFile == null ? null : Path.GetDirectoryName(solutionFile);
        var root = otherDir == null ? null : Path.GetDirectoryName(otherDir);
        if (root == null || !Directory.Exists(root)) return loaded;

        var index = loaded.ToDictionary(
            pair => pair.Key,
            pair => new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase));

        var entitiesDir = Path.Combine(root, "Entities");
        if (Directory.Exists(entitiesDir))
        {
            foreach (var entityDir in Directory.GetDirectories(entitiesDir))
            {
                var entityFile = Path.Combine(entityDir, "Entity.xml");
                if (!File.Exists(entityFile)) continue;

                // The entity name lives inside Entity.xml; the folder name is only a fallback.
                Add(index, ComponentType.Entity, TryReadEntityName(entityFile) ?? Path.GetFileName(entityDir));
            }
        }

        foreach (var folder in new[] { "Dashboards", "Dialogs" })
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.xml"))
            {
                var formId = TryReadFormId(file);
                if (formId != null) Add(index, ComponentType.SystemForm, formId);
            }
        }

        return index;
    }

    /// <summary>
    /// JS web resources may be produced by a separate ScriptLibrary project and copied into the
    /// solution at build time, so their presence in source cannot be judged either way.
    /// </summary>
    private static bool IsScriptLibraryCandidate(ComponentType type, string identity) =>
        type == ComponentType.WebResource && identity.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

    private static string? TryReadFormId(string filePath)
    {
        try
        {
            return XDocument.Load(filePath).Root?.Element("FormId")?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? TryReadEntityName(string filePath)
    {
        try
        {
            var root = XDocument.Load(filePath).Root;
            return root?.Element("EntityInfo")?.Element("entity")?.Attribute("Name")?.Value
                ?? root?.Element("Name")?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static void Add(Dictionary<ComponentType, HashSet<string>> index, ComponentType type, string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return;

        if (!index.TryGetValue(type, out var identifiers))
        {
            identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            index[type] = identifiers;
        }

        identifiers.Add(Normalize(type, identity!));
    }

    private static void ValidateMissingDependencies(Solution solution, List<ValidationResult> results)
    {
        var solutionFile = solution.Source?.FilePath;
        if (solutionFile == null || !File.Exists(solutionFile)) return;

        XDocument document;
        try
        {
            document = XDocument.Load(solutionFile);
        }
        catch (System.Xml.XmlException)
        {
            return;
        }

        var manifest = document.Root?.Element("SolutionManifest");
        if (manifest?.Element("MissingDependencies") != null) return;

        results.Add(new ValidationResult(
            ValidationSeverity.Error,
            $"Solution '{solution.UniqueName}' has no <MissingDependencies/> element in Solution.xml. " +
            "The manifest was authored without the dependency section, which fails with an opaque error when importing into a fresh environment.",
            solutionFile, solution.Source?.Line, solution.Source?.Column)
        {
            Stage = ValidationStage.SolutionManifest,
            Code = ValidationDiagnostics.MissingDependenciesElementAbsent
        });
    }

    private static void ValidateDeclaredComponentsExist(
        Solution solution,
        Dictionary<ComponentType, HashSet<string>> loaded,
        List<ValidationResult> results)
    {
        foreach (var rootComponent in solution.RootComponents)
        {
            if (!ComparableTypes.Contains(rootComponent.Type)) continue;

            var identity = GetIdentity(rootComponent);
            if (identity == null) continue;
            if (IsScriptLibraryCandidate(rootComponent.Type, identity)) continue;

            if (loaded.TryGetValue(rootComponent.Type, out var identifiers) && identifiers.Contains(identity)) continue;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                $"Solution '{solution.UniqueName}' declares a {rootComponent.Type} root component '{identity}' " +
                "that has no matching file in the solution. The packager fails, or the import succeeds with the component silently absent.",
                solution.Source?.FilePath, solution.Source?.Line, solution.Source?.Column)
            {
                Stage = ValidationStage.SolutionManifest,
                Code = ValidationDiagnostics.RootComponentFileAbsent
            });
        }
    }

    private static void ValidateComponentsAreDeclared(
        Solution solution,
        Dictionary<ComponentType, HashSet<string>> loaded,
        List<ValidationResult> results)
    {
        var declared = new Dictionary<ComponentType, HashSet<string>>();
        foreach (var rootComponent in solution.RootComponents)
        {
            var identity = GetIdentity(rootComponent);
            if (identity == null) continue;

            if (!declared.TryGetValue(rootComponent.Type, out var identifiers))
            {
                identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                declared[rootComponent.Type] = identifiers;
            }

            identifiers.Add(identity);
        }

        foreach (var type in AlwaysRootTypes)
        {
            if (!loaded.TryGetValue(type, out var onDisk)) continue;

            declared.TryGetValue(type, out var inManifest);

            foreach (var identity in onDisk)
            {
                if (inManifest != null && inManifest.Contains(identity)) continue;
                if (IsScriptLibraryCandidate(type, identity)) continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    $"{type} '{identity}' exists in the solution source but is not declared as a root component " +
                    $"in solution '{solution.UniqueName}'. It will not be part of the exported solution.",
                    solution.Source?.FilePath, solution.Source?.Line, solution.Source?.Column)
                {
                    Stage = ValidationStage.SolutionManifest,
                    Code = ValidationDiagnostics.ComponentNotDeclaredAsRootComponent
                });
            }
        }
    }

    private static string? GetIdentity(RootComponent rootComponent)
    {
        if (!string.IsNullOrWhiteSpace(rootComponent.SchemaName)) return Normalize(rootComponent.Type, rootComponent.SchemaName!);
        if (rootComponent.Id.HasValue) return Normalize(rootComponent.Type, rootComponent.Id.Value.ToString());
        return null;
    }

    private static string Normalize(ComponentType type, string value)
    {
        var normalized = value.Trim().Trim('{', '}');

        // Plugin assemblies are declared by full assembly name whose Version is stamped at build
        // time, so only the simple name is stable enough to compare.
        if (type == ComponentType.PluginAssembly)
        {
            var comma = normalized.IndexOf(',');
            if (comma > 0) normalized = normalized.Substring(0, comma).Trim();
            if (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);
        }

        return normalized;
    }
}
