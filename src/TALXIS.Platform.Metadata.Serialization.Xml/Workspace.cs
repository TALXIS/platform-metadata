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

    public void AddEntity(EntityMetadata entity) => _entities.Add(entity);
    public void AddGlobalOptionSet(OptionSetMetadata optionSet) => _globalOptionSets.Add(optionSet);
    public void AddRelationship(RelationshipMetadata relationship) => _relationships.Add(relationship);
    public void AddForm(FormMetadata form) => _forms.Add(form);
    public void AddView(SavedQueryMetadata view) => _views.Add(view);
    public void AddPluginAssembly(PluginAssemblyMetadata pluginAssembly) => _pluginAssemblies.Add(pluginAssembly);
    public void AddSdkMessageProcessingStep(SdkMessageProcessingStepMetadata step) => _sdkMessageProcessingSteps.Add(step);
    public void AddSecurityRole(SecurityRoleMetadata securityRole) => _securityRoles.Add(securityRole);
    public void AddAppModule(AppModuleMetadata appModule) => _appModules.Add(appModule);
    public void AddSiteMap(SiteMapMetadata siteMap) => _siteMaps.Add(siteMap);
    public void AddWebResource(WebResourceMetadata webResource) => _webResources.Add(webResource);
    public void AddWorkflow(WorkflowMetadata workflow) => _workflows.Add(workflow);
    public void AddFlowDefinition(FlowDefinitionMetadata flowDefinition) => _flowDefinitions.Add(flowDefinition);
    public void AddGenericComponent(GenericComponentMetadata component) => _genericComponents.Add(component);

    public EntityMetadata? FindEntity(string logicalName) =>
        _entities.FirstOrDefault(e => string.Equals(e.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));
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
