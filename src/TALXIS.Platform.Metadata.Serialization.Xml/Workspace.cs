using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Container for all metadata loaded from a SolutionPackager workspace directory.
/// </summary>
public sealed class Workspace
{
    public string RootPath { get; }

    public Workspace(string rootPath)
    {
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
    }
    public Solution? Solution { get; set; }

    // Entities, option sets, relationships
    private readonly List<EntityMetadata> _entities = new();
    public IReadOnlyList<EntityMetadata> Entities => _entities;

    private readonly List<OptionSetMetadata> _globalOptionSets = new();
    public IReadOnlyList<OptionSetMetadata> GlobalOptionSets => _globalOptionSets;

    private readonly List<RelationshipMetadata> _relationships = new();
    public IReadOnlyList<RelationshipMetadata> Relationships => _relationships;

    // Forms, views
    private readonly List<FormMetadata> _forms = new();
    public IReadOnlyList<FormMetadata> Forms => _forms;

    private readonly List<SavedQueryMetadata> _views = new();
    public IReadOnlyList<SavedQueryMetadata> Views => _views;

    // Plugins, SDK message processing steps
    private readonly List<PluginAssemblyMetadata> _pluginAssemblies = new();
    public IReadOnlyList<PluginAssemblyMetadata> PluginAssemblies => _pluginAssemblies;

    private readonly List<SdkMessageProcessingStepMetadata> _sdkMessageProcessingSteps = new();
    public IReadOnlyList<SdkMessageProcessingStepMetadata> SdkMessageProcessingSteps => _sdkMessageProcessingSteps;

    // Security roles, app modules, site maps, web resources, workflows
    private readonly List<SecurityRoleMetadata> _securityRoles = new();
    public IReadOnlyList<SecurityRoleMetadata> SecurityRoles => _securityRoles;

    private readonly List<AppModuleMetadata> _appModules = new();
    public IReadOnlyList<AppModuleMetadata> AppModules => _appModules;

    private readonly List<SiteMapMetadata> _siteMaps = new();
    public IReadOnlyList<SiteMapMetadata> SiteMaps => _siteMaps;

    private readonly List<WebResourceMetadata> _webResources = new();
    public IReadOnlyList<WebResourceMetadata> WebResources => _webResources;

    private readonly List<WorkflowMetadata> _workflows = new();
    public IReadOnlyList<WorkflowMetadata> Workflows => _workflows;

    private readonly List<FlowDefinitionMetadata> _flowDefinitions = new();
    public IReadOnlyList<FlowDefinitionMetadata> FlowDefinitions => _flowDefinitions;

    // Generic components (no dedicated loader)
    private readonly List<GenericComponentMetadata> _genericComponents = new();
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

    public void AddEntity(EntityMetadata entity) =>
        AddUnique(_entities, entity, e => e.LogicalName, "entity");

    public void AddGlobalOptionSet(OptionSetMetadata optionSet) =>
        AddUnique(_globalOptionSets, optionSet, o => o.Name, "option set");

    public void AddRelationship(RelationshipMetadata relationship) =>
        AddUnique(_relationships, relationship, r => r.SchemaName, "relationship");

    public void AddForm(FormMetadata form) =>
        AddUnique(_forms, form, f => f.FormId, "form");

    public void AddView(SavedQueryMetadata view) =>
        AddUnique(_views, view, v => v.SavedQueryId, "view");

    public void AddPluginAssembly(PluginAssemblyMetadata pluginAssembly) =>
        AddUnique(_pluginAssemblies, pluginAssembly, p => p.PluginAssemblyId, "plugin assembly");

    public void AddSdkMessageProcessingStep(SdkMessageProcessingStepMetadata step) =>
        AddUnique(_sdkMessageProcessingSteps, step, s => s.SdkMessageProcessingStepId, "SDK message processing step");

    public void AddSecurityRole(SecurityRoleMetadata securityRole) =>
        AddUnique(_securityRoles, securityRole, r => r.RoleId, "security role");

    public void AddAppModule(AppModuleMetadata appModule) =>
        AddUnique(_appModules, appModule, a => a.UniqueName, "app module");

    public void AddSiteMap(SiteMapMetadata siteMap) =>
        AddUnique(_siteMaps, siteMap, s => s.UniqueName, "site map");

    public void AddWebResource(WebResourceMetadata webResource) =>
        AddUnique(_webResources, webResource, w => w.WebResourceId, "web resource");

    public void AddWorkflow(WorkflowMetadata workflow) =>
        AddUnique(_workflows, workflow, w => w.WorkflowId, "workflow");

    public void AddFlowDefinition(FlowDefinitionMetadata flowDefinition) =>
        AddUnique(_flowDefinitions, flowDefinition, f => f.FilePath, "flow definition");

    public void AddGenericComponent(GenericComponentMetadata component) =>
        AddUnique(_genericComponents, component, c => c.FilePath, "generic component");

    public EntityMetadata? FindEntity(string logicalName) =>
        _entities.FirstOrDefault(e => string.Equals(e.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    private static void AddUnique<T>(
        List<T> items,
        T item,
        Func<T, string?> getKey,
        string componentType)
    {
        var key = getKey(item);
        if (string.IsNullOrWhiteSpace(key))
        {
            items.Add(item);
            return;
        }

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
    public string FilePath { get; }
    public string Message { get; }
    public int? Line { get; }
    public int? Column { get; }

    public WorkspaceLoadError(string filePath, string message, int? line = null, int? column = null)
    {
        FilePath = filePath;
        Message = message;
        Line = line;
        Column = column;
    }

    public override string ToString()
    {
        if (Line.HasValue && Column.HasValue)
            return $"{FilePath}({Line},{Column}): {Message}";

        return $"{FilePath}: {Message}";
    }
}
