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

    private readonly List<RibbonMetadata> _ribbons = new();
    public IReadOnlyList<RibbonMetadata> Ribbons => _ribbons;

    // Generic components (no dedicated loader)
    private readonly List<GenericComponentMetadata> _genericComponents = new();
    public IReadOnlyList<GenericComponentMetadata> GenericComponents => _genericComponents;

    private readonly List<WorkspaceLoadError> _loadErrors = new();

    /// <summary>
    /// Errors encountered during workspace loading (malformed XML, missing required elements, etc.).
    /// Callers should check this after Load() to report problems to the user.
    /// </summary>
    public IReadOnlyList<WorkspaceLoadError> LoadErrors => _loadErrors;

    internal void AddLoadError(string filePath, string message) =>
        _loadErrors.Add(new WorkspaceLoadError(filePath, message));

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
    public void AddRibbon(RibbonMetadata ribbon) => _ribbons.Add(ribbon);
    public void AddGenericComponent(GenericComponentMetadata component) => _genericComponents.Add(component);

    public EntityMetadata? FindEntity(string logicalName) =>
        _entities.FirstOrDefault(e => string.Equals(e.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<RelationshipMetadata> FindRelationshipsForEntity(string logicalName) =>
        _relationships.Where(relationship => IsRelationshipParticipant(relationship, logicalName)).ToArray();

    public RibbonMetadata? FindRibbon(string entityLogicalName) =>
        _ribbons.FirstOrDefault(r => string.Equals(r.EntityLogicalName, entityLogicalName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<(ComponentType type, string id, MetadataBase? component)> EnumerateLayerComponents()
    {
        foreach (var entity in _entities)
            yield return (ComponentType.Entity, entity.LogicalName, entity);
        foreach (var optionSet in _globalOptionSets)
            yield return (ComponentType.OptionSet, optionSet.Name, optionSet);
        foreach (var relationship in _relationships)
            yield return (ComponentType.EntityRelationship, relationship.SchemaName, relationship);
        foreach (var form in _forms)
            yield return (ComponentType.SystemForm, form.FormId, form);
        foreach (var view in _views)
            yield return (ComponentType.SavedQuery, view.SavedQueryId, view);
        foreach (var pluginAssembly in _pluginAssemblies)
            yield return (ComponentType.PluginAssembly, pluginAssembly.PluginAssemblyId, pluginAssembly);
        foreach (var step in _sdkMessageProcessingSteps)
            yield return (ComponentType.SdkMessageProcessingStep, step.SdkMessageProcessingStepId, step);
        foreach (var role in _securityRoles)
            yield return (ComponentType.Role, role.RoleId, role);
        foreach (var appModule in _appModules)
            yield return (ComponentType.AppModule, appModule.UniqueName, appModule);
        foreach (var siteMap in _siteMaps)
            yield return (ComponentType.SiteMap, siteMap.UniqueName, siteMap);
        foreach (var webResource in _webResources)
            yield return (ComponentType.WebResource, webResource.WebResourceId, webResource);
        foreach (var workflow in _workflows)
            yield return (ComponentType.Workflow, workflow.WorkflowId, workflow);
        foreach (var ribbon in _ribbons)
            yield return (ComponentType.RibbonCustomization, ribbon.EntityLogicalName ?? "global", ribbon);
        foreach (var component in _genericComponents)
            yield return (ComponentType.GenericComponent, component.Id ?? component.FilePath ?? component.ComponentTypeName, component);
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
}

/// <summary>
/// An error encountered while loading a workspace file.
/// </summary>
public sealed class WorkspaceLoadError
{
    public string FilePath { get; }
    public string Message { get; }

    public WorkspaceLoadError(string filePath, string message)
    {
        FilePath = filePath;
        Message = message;
    }

    public override string ToString() => $"{FilePath}: {Message}";
}
