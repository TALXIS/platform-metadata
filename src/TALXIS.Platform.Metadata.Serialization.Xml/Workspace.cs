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

    /// <summary>
    /// Gets or sets the loaded solution metadata from <c>Solution.xml</c>.
    /// </summary>
    public Solution? Solution { get; set; }

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
    /// Keys: "Solution.xml", "Entity:{logicalName}", "OptionSet:{name}", "Relationships.xml"
    /// </summary>
    internal Dictionary<string, XDocument> OriginalDocuments { get; } = new();

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
    /// Enumerates components in a shape suitable for solution-layer import.
    /// </summary>
    public IEnumerable<LayerComponentDescriptor> EnumerateLayerComponents()
    {
        foreach (var entity in _entities)
            yield return new LayerComponentDescriptor(ComponentType.Entity, entity.LogicalName, entity);
        foreach (var optionSet in _globalOptionSets)
            yield return new LayerComponentDescriptor(ComponentType.OptionSet, optionSet.Name, optionSet);
        foreach (var relationship in _relationships)
            yield return new LayerComponentDescriptor(ComponentType.EntityRelationship, relationship.SchemaName, relationship);
        foreach (var form in _forms)
            yield return new LayerComponentDescriptor(ComponentType.SystemForm, form.FormId, form);
        foreach (var view in _views)
            yield return new LayerComponentDescriptor(ComponentType.SavedQuery, view.SavedQueryId, view);
        foreach (var pluginAssembly in _pluginAssemblies)
            yield return new LayerComponentDescriptor(ComponentType.PluginAssembly, pluginAssembly.PluginAssemblyId, pluginAssembly);
        foreach (var step in _sdkMessageProcessingSteps)
            yield return new LayerComponentDescriptor(ComponentType.SdkMessageProcessingStep, step.SdkMessageProcessingStepId, step);
        foreach (var role in _securityRoles)
            yield return new LayerComponentDescriptor(ComponentType.Role, role.RoleId, role);
        foreach (var appModule in _appModules)
            yield return new LayerComponentDescriptor(ComponentType.AppModule, appModule.UniqueName, appModule);
        foreach (var siteMap in _siteMaps)
            yield return new LayerComponentDescriptor(ComponentType.SiteMap, siteMap.UniqueName, siteMap);
        foreach (var webResource in _webResources)
            yield return new LayerComponentDescriptor(ComponentType.WebResource, webResource.WebResourceId, webResource);
        foreach (var workflow in _workflows)
            yield return new LayerComponentDescriptor(ComponentType.Workflow, workflow.WorkflowId, workflow);
        foreach (var ribbon in _ribbons)
            yield return new LayerComponentDescriptor(ComponentType.RibbonCustomization, ribbon.EntityLogicalName ?? "global", ribbon);
        foreach (var flowDefinition in _flowDefinitions)
            yield return new LayerComponentDescriptor(ComponentType.GenericComponent, flowDefinition.FilePath ?? flowDefinition.Name ?? "flow", flowDefinition);
        foreach (var component in _genericComponents)
            yield return new LayerComponentDescriptor(ComponentType.GenericComponent, component.FilePath ?? component.Id ?? component.ComponentTypeName, component);
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
