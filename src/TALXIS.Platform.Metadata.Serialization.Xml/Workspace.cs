using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Container for all metadata loaded from a SolutionPackager workspace directory.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Gets the root directory the workspace was loaded from.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Creates a workspace rooted at the supplied directory.
    /// </summary>
    /// <param name="rootPath">Path to the unpacked SolutionPackager workspace.</param>
    public Workspace(string rootPath)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
    }

    private readonly List<Solution> _solutions = new();
    private readonly List<SolutionComponentMembership> _solutionComponents = new();
    private readonly List<ComponentSourceSnapshot> _componentSources = new();

    /// <summary>
    /// Gets the loaded solution manifests.
    /// </summary>
    public IReadOnlyList<Solution> Solutions => _solutions;

    /// <summary>
    /// Gets solution/component membership rows, separate from component layers.
    /// </summary>
    public IReadOnlyList<SolutionComponentMembership> SolutionComponentMemberships => _solutionComponents;

    /// <summary>
    /// Gets source-owned component snapshots loaded from solution projects.
    /// </summary>
    public IReadOnlyList<ComponentSourceSnapshot> ComponentSourceSnapshots => _componentSources;

    /// <summary>
    /// Gets the Dataverse-style component layer manager for this workspace.
    /// </summary>
    public SolutionLayerManager Layers { get; } = new();

    // Entities, option sets, relationships
    private readonly List<EntityMetadata> _entities = new();

    /// <summary>
    /// Gets the loaded entities.
    /// </summary>
    public IReadOnlyList<EntityMetadata> Entities => _entities;

    private readonly List<OptionSetMetadata> _globalOptionSets = new();

    /// <summary>
    /// Gets the loaded global option sets.
    /// </summary>
    public IReadOnlyList<OptionSetMetadata> GlobalOptionSets => _globalOptionSets;

    private readonly List<RelationshipMetadata> _relationships = new();

    /// <summary>
    /// Gets the loaded relationships.
    /// </summary>
    public IReadOnlyList<RelationshipMetadata> Relationships => _relationships;

    // Forms, views
    private readonly List<FormMetadata> _forms = new();

    /// <summary>
    /// Gets the loaded forms.
    /// </summary>
    public IReadOnlyList<FormMetadata> Forms => _forms;

    private readonly List<SavedQueryMetadata> _views = new();

    /// <summary>
    /// Gets the loaded views.
    /// </summary>
    public IReadOnlyList<SavedQueryMetadata> Views => _views;

    // Plugins, SDK message processing steps
    private readonly List<PluginAssemblyMetadata> _pluginAssemblies = new();

    /// <summary>
    /// Gets the loaded plugin assemblies.
    /// </summary>
    public IReadOnlyList<PluginAssemblyMetadata> PluginAssemblies => _pluginAssemblies;

    private readonly List<SdkMessageProcessingStepMetadata> _sdkMessageProcessingSteps = new();

    /// <summary>
    /// Gets the loaded SDK message processing steps.
    /// </summary>
    public IReadOnlyList<SdkMessageProcessingStepMetadata> SdkMessageProcessingSteps => _sdkMessageProcessingSteps;

    // Security roles, app modules, site maps, web resources, workflows
    private readonly List<SecurityRoleMetadata> _securityRoles = new();

    /// <summary>
    /// Gets the loaded security roles.
    /// </summary>
    public IReadOnlyList<SecurityRoleMetadata> SecurityRoles => _securityRoles;

    private readonly List<AppModuleMetadata> _appModules = new();

    /// <summary>
    /// Gets the loaded app modules.
    /// </summary>
    public IReadOnlyList<AppModuleMetadata> AppModules => _appModules;

    private readonly List<SiteMapMetadata> _siteMaps = new();

    /// <summary>
    /// Gets the loaded site maps.
    /// </summary>
    public IReadOnlyList<SiteMapMetadata> SiteMaps => _siteMaps;

    private readonly List<WebResourceMetadata> _webResources = new();

    /// <summary>
    /// Gets the loaded web resources.
    /// </summary>
    public IReadOnlyList<WebResourceMetadata> WebResources => _webResources;

    private readonly List<WorkflowMetadata> _workflows = new();

    /// <summary>
    /// Gets the loaded workflows.
    /// </summary>
    public IReadOnlyList<WorkflowMetadata> Workflows => _workflows;

    private readonly List<RibbonMetadata> _ribbons = new();

    /// <summary>
    /// Gets the loaded ribbon customizations.
    /// </summary>
    public IReadOnlyList<RibbonMetadata> Ribbons => _ribbons;

    private readonly List<FlowDefinitionMetadata> _flowDefinitions = new();

    /// <summary>
    /// Gets the parsed flow-definition JSON documents.
    /// </summary>
    public IReadOnlyList<FlowDefinitionMetadata> FlowDefinitions => _flowDefinitions;

    // Generic components (no dedicated loader)
    private readonly List<GenericComponentMetadata> _genericComponents = new();

    /// <summary>
    /// Gets components that were loaded generically without a dedicated typed loader.
    /// </summary>
    public IReadOnlyList<GenericComponentMetadata> GenericComponents => _genericComponents;

    private readonly List<WorkspaceLoadError> _loadErrors = new();

    /// <summary>
    /// Errors encountered during workspace loading (malformed XML, missing required elements, etc.).
    /// Callers should check this after Load() to report problems to the user.
    /// </summary>
    public IReadOnlyList<WorkspaceLoadError> LoadErrors => _loadErrors;

    internal void AddLoadError(string filePath, string message, int? line = null, int? column = null) =>
        _loadErrors.Add(new WorkspaceLoadError(filePath, message, line, column));

    /// <summary>
    /// Original XML documents stored by the reader for roundtrip-safe writing.
    /// Keys include "Solution:{uniqueName}:Solution.xml", "Entity:{logicalName}", "OptionSet:{name}", "Relationships.xml"
    /// </summary>
    internal Dictionary<string, XDocument> OriginalDocuments { get; } = new();

    /// <summary>
    /// Adds a solution manifest to the workspace and rejects duplicate unique names.
    /// </summary>
    public void AddSolution(Solution solution) =>
        AddUnique(_solutions, solution, s => s.UniqueName, "solution");

    /// <summary>
    /// Finds a solution by unique name using case-insensitive comparison.
    /// </summary>
    public Solution? FindSolution(string uniqueName) =>
        _solutions.FirstOrDefault(s => string.Equals(s.UniqueName, uniqueName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes a solution and its membership/source/layer records.
    /// </summary>
    public bool RemoveSolution(string uniqueName)
    {
        var solution = FindSolution(uniqueName);
        if (solution == null)
            return false;

        _solutions.Remove(solution);
        _solutionComponents.RemoveAll(m => string.Equals(m.SolutionUniqueName, uniqueName, StringComparison.OrdinalIgnoreCase));
        _componentSources.RemoveAll(s => string.Equals(s.SourceSolutionUniqueName, uniqueName, StringComparison.OrdinalIgnoreCase));
        Layers.RemoveSolutionLayers(uniqueName);
        return true;
    }

    /// <summary>
    /// Adds a solution membership row.
    /// </summary>
    public void AddSolutionComponentMembership(SolutionComponentMembership membership)
    {
        if (membership == null) throw new ArgumentNullException(nameof(membership));
        _solutionComponents.Add(membership);
    }

    /// <summary>
    /// Adds a source-owned component snapshot.
    /// </summary>
    public void AddComponentSourceSnapshot(ComponentSourceSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        _componentSources.Add(snapshot);
    }

    /// <summary>
    /// Registers a solution project's memberships, source snapshots, and Dataverse-like layer state.
    /// </summary>
    /// <param name="solution">Solution manifest that owns the source project.</param>
    /// <param name="importOrder">Caller-defined import/source order.</param>
    /// <param name="sourceRootPath">Optional source project root for diagnostics and write-back.</param>
    /// <param name="components">Components loaded from the source project.</param>
    public void RegisterSolutionSource(Solution solution, int importOrder, string? sourceRootPath, IEnumerable<LayerComponentDescriptor> components)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));
        if (components == null) throw new ArgumentNullException(nameof(components));

        var componentList = components.ToArray();

        foreach (var rootComponent in solution.RootComponents)
        {
            var objectId = GetRootComponentObjectId(rootComponent);
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            AddSolutionComponentMembership(new SolutionComponentMembership
            {
                SolutionUniqueName = solution.UniqueName,
                Identity = new ComponentIdentity(rootComponent.Type, objectId!),
                RootComponentBehavior = rootComponent.BehaviorOption,
                SourceRootPath = sourceRootPath,
                SourceDocumentKey = GetSolutionDocumentKey(solution.UniqueName)
            });
        }

        foreach (var component in componentList)
        {
            AddComponentSourceSnapshot(new ComponentSourceSnapshot
            {
                SourceSolutionUniqueName = solution.UniqueName,
                Identity = component.Identity,
                Metadata = component.Metadata,
                SourceRootPath = sourceRootPath,
                SourceDocumentKey = component.SourceDocumentKey,
                SourceOrder = importOrder,
                IsManaged = solution.IsManaged
            });
        }

        if (solution.IsManaged)
            Layers.ImportManagedLayer(solution, importOrder, componentList, sourceRootPath);
        else
            Layers.ImportActiveLayerSnapshot(solution, importOrder, componentList, sourceRootPath);
    }

    internal static string GetSolutionDocumentKey(string uniqueName) => $"Solution:{uniqueName}:Solution.xml";

    /// <summary>
    /// Adds an entity to the workspace and rejects duplicate logical names.
    /// </summary>
    public void AddEntity(EntityMetadata entity) =>
        AddUnique(_entities, entity, e => e.LogicalName, "entity");

    /// <summary>
    /// Adds a global option set to the workspace and rejects duplicate names.
    /// </summary>
    public void AddGlobalOptionSet(OptionSetMetadata optionSet) =>
        AddUnique(_globalOptionSets, optionSet, o => o.Name, "option set");

    /// <summary>
    /// Adds a relationship to the workspace and rejects duplicate schema names.
    /// </summary>
    public void AddRelationship(RelationshipMetadata relationship) =>
        AddUnique(_relationships, relationship, r => r.SchemaName, "relationship");

    /// <summary>
    /// Adds a form to the workspace and rejects duplicate form IDs.
    /// </summary>
    public void AddForm(FormMetadata form) =>
        AddUnique(_forms, form, f => f.FormId, "form");

    /// <summary>
    /// Adds a view to the workspace and rejects duplicate saved-query IDs.
    /// </summary>
    public void AddView(SavedQueryMetadata view) =>
        AddUnique(_views, view, v => v.SavedQueryId, "view");

    /// <summary>
    /// Adds a plugin assembly to the workspace and rejects duplicate IDs.
    /// </summary>
    public void AddPluginAssembly(PluginAssemblyMetadata pluginAssembly) =>
        AddUnique(_pluginAssemblies, pluginAssembly, p => p.PluginAssemblyId, "plugin assembly");

    /// <summary>
    /// Adds an SDK message processing step to the workspace and rejects duplicate IDs.
    /// </summary>
    public void AddSdkMessageProcessingStep(SdkMessageProcessingStepMetadata step) =>
        AddUnique(_sdkMessageProcessingSteps, step, s => s.SdkMessageProcessingStepId, "SDK message processing step");

    /// <summary>
    /// Adds a security role to the workspace and rejects duplicate role IDs.
    /// </summary>
    public void AddSecurityRole(SecurityRoleMetadata securityRole) =>
        AddUnique(_securityRoles, securityRole, r => r.RoleId, "security role");

    /// <summary>
    /// Adds an app module to the workspace and rejects duplicate unique names.
    /// </summary>
    public void AddAppModule(AppModuleMetadata appModule) =>
        AddUnique(_appModules, appModule, a => a.UniqueName, "app module");

    /// <summary>
    /// Adds a site map to the workspace and rejects duplicate unique names.
    /// </summary>
    public void AddSiteMap(SiteMapMetadata siteMap) =>
        AddUnique(_siteMaps, siteMap, s => s.UniqueName, "site map");

    /// <summary>
    /// Adds a web resource to the workspace and rejects duplicate IDs.
    /// </summary>
    public void AddWebResource(WebResourceMetadata webResource) =>
        AddUnique(_webResources, webResource, w => w.WebResourceId, "web resource");

    /// <summary>
    /// Adds a workflow to the workspace and rejects duplicate IDs.
    /// </summary>
    public void AddWorkflow(WorkflowMetadata workflow) =>
        AddUnique(_workflows, workflow, w => w.WorkflowId, "workflow");

    /// <summary>
    /// Adds a ribbon customization to the workspace and rejects duplicate target entities.
    /// </summary>
    public void AddRibbon(RibbonMetadata ribbon) =>
        AddUnique(_ribbons, ribbon, r => r.EntityLogicalName, "ribbon");

    /// <summary>
    /// Adds a parsed flow-definition JSON document to the workspace and rejects duplicate file paths.
    /// </summary>
    public void AddFlowDefinition(FlowDefinitionMetadata flowDefinition) =>
        AddUnique(_flowDefinitions, flowDefinition, f => f.FilePath, "flow definition");

    /// <summary>
    /// Adds a generic component to the workspace and rejects duplicate file paths.
    /// </summary>
    public void AddGenericComponent(GenericComponentMetadata component) =>
        AddUnique(_genericComponents, component, c => c.FilePath, "generic component");

    /// <summary>
    /// Finds an entity by logical name using case-insensitive comparison.
    /// </summary>
    public EntityMetadata? FindEntity(string logicalName) =>
        _entities.FirstOrDefault(e => string.Equals(e.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds relationships that reference the supplied entity.
    /// </summary>
    public IReadOnlyList<RelationshipMetadata> FindRelationshipsForEntity(string logicalName) =>
        _relationships.Where(relationship => IsRelationshipParticipant(relationship, logicalName)).ToArray();

    /// <summary>
    /// Finds a ribbon customization by target entity logical name.
    /// </summary>
    public RibbonMetadata? FindRibbon(string entityLogicalName) =>
        _ribbons.FirstOrDefault(r => string.Equals(r.EntityLogicalName, entityLogicalName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes an entity together with its forms, views, and ribbon customization.
    /// </summary>
    public bool RemoveEntity(string logicalName)
    {
        var entity = FindEntity(logicalName);
        if (entity == null) return false;

        _entities.Remove(entity);
        OriginalDocuments.Remove($"Entity:{entity.LogicalName}");

        foreach (var form in _forms.Where(f => string.Equals(f.EntityLogicalName, logicalName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            RemoveForm(form.FormId);
        }

        foreach (var view in _views.Where(v => string.Equals(v.EntityLogicalName, logicalName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            RemoveView(view.SavedQueryId);
        }

        RemoveRibbon(logicalName);
        return true;
    }

    /// <summary>
    /// Removes a global option set by name.
    /// </summary>
    public bool RemoveGlobalOptionSet(string name) =>
        RemoveByKey(_globalOptionSets, o => o.Name, name, o => $"OptionSet:{o.Name}");

    /// <summary>
    /// Removes a relationship by schema name.
    /// </summary>
    public bool RemoveRelationship(string schemaName) =>
        RemoveByKey(_relationships, r => r.SchemaName, schemaName, documentKey: null);

    /// <summary>
    /// Removes a form by form ID.
    /// </summary>
    public bool RemoveForm(string formId) =>
        RemoveByKey(_forms, f => f.FormId, formId, f => $"Form:{f.EntityLogicalName}:{f.FormId}");

    /// <summary>
    /// Removes a view by saved-query ID.
    /// </summary>
    public bool RemoveView(string savedQueryId) =>
        RemoveByKey(_views, v => v.SavedQueryId, savedQueryId, v => $"View:{v.EntityLogicalName}:{v.SavedQueryId}");

    /// <summary>
    /// Removes a plugin assembly by ID.
    /// </summary>
    public bool RemovePluginAssembly(string pluginAssemblyId) =>
        RemoveByKey(_pluginAssemblies, p => p.PluginAssemblyId, pluginAssemblyId, p => $"PluginAssembly:{p.Name}");

    /// <summary>
    /// Removes an SDK message processing step by ID.
    /// </summary>
    public bool RemoveSdkMessageProcessingStep(string stepId) =>
        RemoveByKey(_sdkMessageProcessingSteps, s => s.SdkMessageProcessingStepId, stepId, s => $"Step:{s.SdkMessageProcessingStepId}");

    /// <summary>
    /// Removes a security role by role ID.
    /// </summary>
    public bool RemoveSecurityRole(string roleId) =>
        RemoveByKey(_securityRoles, r => r.RoleId, roleId, r => $"Role:{r.RoleId}");

    /// <summary>
    /// Removes an app module by unique name.
    /// </summary>
    public bool RemoveAppModule(string uniqueName) =>
        RemoveByKey(_appModules, a => a.UniqueName, uniqueName, a => $"AppModule:{a.UniqueName}");

    /// <summary>
    /// Removes a site map by unique name.
    /// </summary>
    public bool RemoveSiteMap(string uniqueName) =>
        RemoveByKey(_siteMaps, s => s.UniqueName, uniqueName, s => $"SiteMap:{s.UniqueName}");

    /// <summary>
    /// Removes a web resource by ID.
    /// </summary>
    public bool RemoveWebResource(string webResourceId) =>
        RemoveByKey(_webResources, w => w.WebResourceId, webResourceId, w => $"WebResource:{w.Name}");

    /// <summary>
    /// Removes a workflow by ID.
    /// </summary>
    public bool RemoveWorkflow(string workflowId) =>
        RemoveByKey(_workflows, w => w.WorkflowId, workflowId, w => $"Workflow:{w.WorkflowId}");

    /// <summary>
    /// Removes a ribbon customization by target entity logical name.
    /// </summary>
    public bool RemoveRibbon(string entityLogicalName) =>
        RemoveByKey(_ribbons, r => r.EntityLogicalName, entityLogicalName, r => $"Ribbon:{r.EntityLogicalName}");

    private bool RemoveByKey<T>(List<T> items, Func<T, string?> getKey, string key, Func<T, string>? documentKey)
    {
        var index = items.FindIndex(item => string.Equals(getKey(item), key, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        if (documentKey != null) OriginalDocuments.Remove(documentKey(items[index]));
        items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Enumerates components in a shape suitable for solution-layer import.
    /// </summary>
    public IEnumerable<LayerComponentDescriptor> EnumerateLayerComponents()
    {
        foreach (var entity in _entities)
            yield return new LayerComponentDescriptor(ComponentType.Entity, entity.LogicalName, entity, $"Entity:{entity.LogicalName}");
        foreach (var optionSet in _globalOptionSets)
            yield return new LayerComponentDescriptor(ComponentType.OptionSet, optionSet.Name, optionSet, $"OptionSet:{optionSet.Name}");
        foreach (var relationship in _relationships)
            yield return new LayerComponentDescriptor(ComponentType.EntityRelationship, relationship.SchemaName, relationship, $"Relationship:{relationship.SchemaName}");
        foreach (var form in _forms)
            yield return new LayerComponentDescriptor(ComponentType.SystemForm, form.FormId, form, $"Form:{form.EntityLogicalName}:{form.FormId}");
        foreach (var view in _views)
            yield return new LayerComponentDescriptor(ComponentType.SavedQuery, view.SavedQueryId, view, $"View:{view.EntityLogicalName}:{view.SavedQueryId}");
        foreach (var pluginAssembly in _pluginAssemblies)
            yield return new LayerComponentDescriptor(ComponentType.PluginAssembly, pluginAssembly.PluginAssemblyId, pluginAssembly, $"PluginAssembly:{pluginAssembly.Name}");
        foreach (var step in _sdkMessageProcessingSteps)
            yield return new LayerComponentDescriptor(ComponentType.SdkMessageProcessingStep, step.SdkMessageProcessingStepId, step, $"Step:{step.SdkMessageProcessingStepId}");
        foreach (var role in _securityRoles)
            yield return new LayerComponentDescriptor(ComponentType.Role, role.RoleId, role, $"Role:{role.RoleId}");
        foreach (var appModule in _appModules)
            yield return new LayerComponentDescriptor(ComponentType.AppModule, appModule.UniqueName, appModule, $"AppModule:{appModule.UniqueName}");
        foreach (var siteMap in _siteMaps)
            yield return new LayerComponentDescriptor(ComponentType.SiteMap, siteMap.UniqueName, siteMap, $"SiteMap:{siteMap.UniqueName}");
        foreach (var webResource in _webResources)
            yield return new LayerComponentDescriptor(ComponentType.WebResource, webResource.WebResourceId, webResource, $"WebResource:{webResource.Name}");
        foreach (var workflow in _workflows)
            yield return new LayerComponentDescriptor(ComponentType.Workflow, workflow.WorkflowId, workflow, $"Workflow:{workflow.WorkflowId}");
        foreach (var ribbon in _ribbons)
            yield return new LayerComponentDescriptor(ComponentType.RibbonCustomization, ribbon.EntityLogicalName ?? "global", ribbon, $"Ribbon:{ribbon.EntityLogicalName ?? "global"}");
        foreach (var flowDefinition in _flowDefinitions)
            yield return new LayerComponentDescriptor(ComponentType.GenericComponent, flowDefinition.FilePath ?? flowDefinition.Name ?? "flow", flowDefinition, $"FlowDefinition:{flowDefinition.FilePath ?? flowDefinition.Name ?? "flow"}");
        foreach (var component in _genericComponents)
            yield return new LayerComponentDescriptor(ComponentType.GenericComponent, component.FilePath ?? component.Id ?? component.ComponentTypeName ?? "generic", component, $"Generic:{component.FilePath ?? component.Id ?? component.ComponentTypeName ?? "generic"}");
    }

    internal void MergeComponentsFrom(Workspace source, bool preferSource)
    {
        MergeUnique(_entities, source.Entities, e => e.LogicalName, preferSource);
        MergeUnique(_globalOptionSets, source.GlobalOptionSets, o => o.Name, preferSource);
        MergeUnique(_relationships, source.Relationships, r => r.SchemaName, preferSource);
        MergeUnique(_forms, source.Forms, f => f.FormId, preferSource);
        MergeUnique(_views, source.Views, v => v.SavedQueryId, preferSource);
        MergeUnique(_pluginAssemblies, source.PluginAssemblies, p => p.PluginAssemblyId, preferSource);
        MergeUnique(_sdkMessageProcessingSteps, source.SdkMessageProcessingSteps, s => s.SdkMessageProcessingStepId, preferSource);
        MergeUnique(_securityRoles, source.SecurityRoles, r => r.RoleId, preferSource);
        MergeUnique(_appModules, source.AppModules, a => a.UniqueName, preferSource);
        MergeUnique(_siteMaps, source.SiteMaps, s => s.UniqueName, preferSource);
        MergeUnique(_webResources, source.WebResources, w => w.WebResourceId, preferSource);
        MergeUnique(_workflows, source.Workflows, w => w.WorkflowId, preferSource);
        MergeUnique(_ribbons, source.Ribbons, r => r.EntityLogicalName, preferSource);
        MergeUnique(_flowDefinitions, source.FlowDefinitions, f => f.FilePath, preferSource);
        MergeUnique(_genericComponents, source.GenericComponents, c => c.FilePath, preferSource);
    }

    internal void CopyOriginalDocumentsFrom(Workspace source)
    {
        foreach (var document in source.OriginalDocuments)
        {
            OriginalDocuments[document.Key] = document.Value;
        }
    }

    internal void CopyLoadErrorsFrom(Workspace source)
    {
        _loadErrors.AddRange(source.LoadErrors);
    }

    private static bool IsRelationshipParticipant(RelationshipMetadata relationship, string logicalName)
    {
        if (relationship is OneToManyRelationshipMetadata oneToMany)
        {
            return string.Equals(oneToMany.ReferencedEntity, logicalName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(oneToMany.ReferencingEntity, logicalName, StringComparison.OrdinalIgnoreCase);
        }

        if (relationship is ManyToManyRelationshipMetadata manyToMany)
        {
            return string.Equals(manyToMany.Entity1LogicalName, logicalName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(manyToMany.Entity2LogicalName, logicalName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? GetRootComponentObjectId(RootComponent component)
    {
        if (component.Id.HasValue)
            return component.Id.Value.ToString("D");

        return component.SchemaName;
    }

    private static void MergeUnique<T>(List<T> target, IReadOnlyList<T> source, Func<T, string?> getKey, bool preferSource)
    {
        foreach (var item in source)
        {
            var key = getKey(item);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var existingIndex = target.FindIndex(existing => string.Equals(getKey(existing), key, StringComparison.OrdinalIgnoreCase));
            if (existingIndex < 0)
            {
                target.Add(item);
            }
            else if (preferSource)
            {
                target[existingIndex] = item;
            }
        }
    }

    private static void AddUnique<T>(
        List<T> items,
        T item,
        Func<T, string?> getKey,
        string componentType)
    {
        var key = getKey(item);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"A {componentType} must have a non-empty identity key before it can be added to the workspace.");

        if (items.Any(existing => string.Equals(getKey(existing), key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A {componentType} with key '{key}' already exists in the workspace.");

        items.Add(item);
    }
}

/// <summary>
/// An error encountered while loading a workspace file.
/// </summary>
public sealed class WorkspaceLoadError
{
    /// <summary>
    /// Gets the file that could not be loaded successfully.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the load error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the 1-based line number when the loader could determine one.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets the 1-based column number when the loader could determine one.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// Creates a load error.
    /// </summary>
    public WorkspaceLoadError(string filePath, string message, int? line = null, int? column = null)
    {
        FilePath = filePath;
        Message = message;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Returns a compiler-style message including file and optional line/column.
    /// </summary>
    public override string ToString()
    {
        if (Line.HasValue && Column.HasValue)
            return $"{FilePath}({Line},{Column}): {Message}";

        return $"{FilePath}: {Message}";
    }
}
