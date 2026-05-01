namespace TALXIS.Platform.Metadata;

/// <summary>
/// Static registry of all known component definitions, pre-populated from SolutionPackager internals.
/// Cross-references Section 1.2 (Processor → XML Element), Section 2.1 (file layout),
/// and Section 6.1 (identity strategies).
/// </summary>
public static class ComponentDefinitionRegistry
{
    private static readonly Dictionary<ComponentType, ComponentDefinition> _byType = new();
    private static readonly Dictionary<string, ComponentDefinition> _byXmlElement = new(StringComparer.OrdinalIgnoreCase);

    static ComponentDefinitionRegistry()
    {
        // Entity — Name-only identity, merge support, subfolders for forms/views/etc.
        Register(new ComponentDefinition(ComponentType.Entity, "Entity", "Entities", "Entities", "$(PrimaryName)/Entity.xml", IdentityStrategy.Name, SupportsMerge: true, HasSubfolders: true));

        // OptionSet — Name-only identity
        Register(new ComponentDefinition(ComponentType.OptionSet, "OptionSet", "optionsets", "OptionSets", "$(PrimaryName)", IdentityStrategy.Name));

        // EntityRelationship — Name-only identity, single shared file
        Register(new ComponentDefinition(ComponentType.EntityRelationship, "EntityRelationship", "EntityRelationships", "Other", "Relationships.xml", IdentityStrategy.Name));

        // SiteMap — Singleton identity, managed variant support
        Register(new ComponentDefinition(ComponentType.SiteMap, "SiteMap", "SiteMap", "Other", "$(type)$(managed).xml", IdentityStrategy.Singleton));

        // RibbonCustomization — Singleton identity
        Register(new ComponentDefinition(ComponentType.RibbonCustomization, "RibbonCustomization", "RibbonDiffXml", "Other", "$(type).xml", IdentityStrategy.Singleton));

        // Role — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.Role, "Role", "Roles", "Roles", "$(PrimaryName)", IdentityStrategy.Guid));

        // ConnectionRole — GUID-only identity
        Register(new ComponentDefinition(ComponentType.ConnectionRole, "ConnectionRole", "ConnectionRoles", "Other", "$(type)s.xml", IdentityStrategy.Guid));

        // Dashboard — GUID as filename
        Register(new ComponentDefinition(ComponentType.Dashboard, "Dashboard", "Dashboards", "Dashboards", "$(PrimaryName)", IdentityStrategy.Guid));

        // FieldSecurityProfile — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.FieldSecurityProfile, "FieldSecurityProfile", "FieldSecurityProfiles", "Other", "$(type)s.xml", IdentityStrategy.Guid));

        // WebResource — GUID + Name identity, file-backed
        Register(new ComponentDefinition(ComponentType.WebResource, "WebResource", "WebResources", "WebResources", "$(PrimaryName)", IdentityStrategy.Guid, IsFileBacked: true));

        // Workflow — GUID + Name identity, single collection file
        Register(new ComponentDefinition(ComponentType.Workflow, "Workflow", "Workflows", "Workflows", "Workflows.xml", IdentityStrategy.Guid));

        // PluginAssembly — GUID + FullName identity
        Register(new ComponentDefinition(ComponentType.PluginAssembly, "PluginAssembly", "SolutionPluginAssemblies", "PluginAssemblies", "PluginAssemblies.xml", IdentityStrategy.Guid));

        // SdkMessageProcessingStep — GUID-only identity
        Register(new ComponentDefinition(ComponentType.SdkMessageProcessingStep, "SdkMessageProcessingStep", "SdkMessageProcessingSteps", "SdkMessageProcessingSteps", "$(PrimaryName)", IdentityStrategy.Guid));

        // ServiceEndpoint — GUID-based
        Register(new ComponentDefinition(ComponentType.ServiceEndpoint, "ServiceEndpoint", "ServiceEndpoints", "PluginAssemblies", "$(type)s.xml", IdentityStrategy.Guid));

        // Report — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.Report, "Report", "Reports", "Reports", "$(type)", IdentityStrategy.Guid));

        // Template — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.Template, "Template", "Templates", "Templates", "$(PrimaryName).xml", IdentityStrategy.Guid));

        // EntityMap — Composite key (Source,Target)
        Register(new ComponentDefinition(ComponentType.EntityMap, "EntityMap", "EntityMaps", "Other", "$(type)s.xml", IdentityStrategy.Composite));

        // ProfileRule — Name identity
        Register(new ComponentDefinition(ComponentType.ProfileRule, "ProfileRule", "ProfileRules", "ChannelAccess", "ProfileRules/$(PrimaryName)", IdentityStrategy.Name));

        // ChannelAccessProfile — Name identity
        Register(new ComponentDefinition(ComponentType.ChannelAccessProfile, "ChannelAccessProfile", "ChannelAccessProfiles", "ChannelAccess", "Profiles/$(PrimaryName)", IdentityStrategy.Name));

        // SdkMessage — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.SdkMessage, "SdkMessage", "SdkMessages", "SdkMessages", "$(PrimaryName)", IdentityStrategy.Guid));

        // ComplexControl — Name identity (qualified name)
        Register(new ComponentDefinition(ComponentType.ComplexControl, "ComplexControl", "ComplexControls", "ComplexControls", "$(PrimaryName).xml", IdentityStrategy.Name));

        // Dialogs — GUID as filename
        Register(new ComponentDefinition(ComponentType.Dialogs, "Dialogs", "Dialogs", "Dialogs", "$(PrimaryName).xml", IdentityStrategy.Guid));

        // StoredProcedure
        Register(new ComponentDefinition(ComponentType.StoredProcedure, "StoredProcedure", "StoredProcedures", "StoredProcedures", "$(type)", IdentityStrategy.Name));

        // AppModule — GUID + Name, managed variant, subfolders
        Register(new ComponentDefinition(ComponentType.AppModule, "AppModule", "AppModules", "AppModules", "$(PrimaryName)/AppModule$(managed).xml", IdentityStrategy.Guid, SupportsMerge: true, HasSubfolders: true));

        // AppModuleSiteMap — managed variant, subfolders
        Register(new ComponentDefinition(ComponentType.AppModuleSiteMap, "AppModuleSiteMap", "AppModuleSiteMaps", "AppModuleSiteMaps", "$(PrimaryName)/AppModuleSiteMap$(managed).xml", IdentityStrategy.Guid, HasSubfolders: true));

        // EntityPrivilege — Singleton-like
        Register(new ComponentDefinition(ComponentType.EntityPrivilege, "EntityPrivilege", "EntityPrivileges", "EntityPrivileges", "$(type)", IdentityStrategy.Singleton, SupportsMerge: true));

        // WebWizard
        Register(new ComponentDefinition(ComponentType.WebWizard, "WebWizard", "WebWizards", "WebWizards", "$(PrimaryName).xml", IdentityStrategy.Guid));

        // SdkMessageFilter / ImportMaps — Name identity
        Register(new ComponentDefinition(ComponentType.SdkMessageFilter, "SdkMessageFilter", "Maps", "Maps", "$(PrimaryName).xml", IdentityStrategy.Name));

        // EntityDataProvider
        Register(new ComponentDefinition(ComponentType.EntityDataProvider, "EntityDataProvider", "EntityDataProviders", "EntityDataProviders", "$(PrimaryName).xml", IdentityStrategy.Name));

        // EntityDataSource
        Register(new ComponentDefinition(ComponentType.EntityDataSource, "EntityDataSource", "EntityDataSources", "EntityDataSources", "$(PrimaryName).xml", IdentityStrategy.Name));

        // InteractionCentricDashboard
        Register(new ComponentDefinition(ComponentType.InteractionCentricDashboard, "InteractionCentricDashboard", "InteractionCentricDashboards", "InteractionCentricDashboards", "$(PrimaryName)", IdentityStrategy.Guid));

        // TeamTemplate
        Register(new ComponentDefinition(ComponentType.TeamTemplate, "TeamTemplate", "TeamTemplates", "TeamTemplates", "$(PrimaryName)", IdentityStrategy.Name));

        // SyncAttributeMappingProfile
        Register(new ComponentDefinition(ComponentType.SyncAttributeMappingProfile, "SyncAttributeMappingProfile", "SyncAttributeMappingProfiles", "SyncAttributeMappingProfiles", "$(PrimaryName)", IdentityStrategy.Name));

        // MobileOfflineProfile
        Register(new ComponentDefinition(ComponentType.MobileOfflineProfile, "MobileOfflineProfile", "MobileOfflineProfiles", "MobileOfflineProfiles", "$(PrimaryName)", IdentityStrategy.Name));

        // CustomControl — Qualified name identity ({namespace}.{constructor})
        Register(new ComponentDefinition(ComponentType.CustomControl, "CustomControl", "CustomControls", "Controls", "$(PrimaryName)", IdentityStrategy.Name));

        // EnvironmentVariableDefinition — Singleton wrapper
        Register(new ComponentDefinition(ComponentType.EnvironmentVariableDefinition, "EnvironmentVariableDefinition", "EnvironmentVariables", "EnvironmentVariables", "$(PrimaryName).xml", IdentityStrategy.Singleton));

        // Connector — dynamic root directory
        Register(new ComponentDefinition(ComponentType.Connector, "Connector", "Connectors", "$(ComponentsRootName)", "$(PrimaryName).xml", IdentityStrategy.Name));

        // OrganizationSettings
        Register(new ComponentDefinition(ComponentType.OrganizationSettings, "OrganizationSettings", "OrganizationSettings", "OrganizationSettings", "_legacy/$(PrimaryName).meta.xml", IdentityStrategy.Name));

        // CanvasApp — file-backed (meta.xml + binary)
        Register(new ComponentDefinition(ComponentType.CanvasApp, "CanvasApp", "CanvasApps", "CanvasApps", "$(PrimaryName).meta.xml", IdentityStrategy.Guid, IsFileBacked: true));

        // ServicePlans — single collection file
        Register(new ComponentDefinition(ComponentType.ServicePlans, "ServicePlans", "serviceplans", "ServicePlans", "ServicePlans.xml", IdentityStrategy.Name));

        // ServicePlanAppModules — single collection file
        Register(new ComponentDefinition(ComponentType.ServicePlanAppModules, "ServicePlanAppModules", "serviceplanappmodulesset", "ServicePlans", "ServicePlanAppModules.xml", IdentityStrategy.Name));

        // ScfComponent — file-backed, identity by ComponentName + SchemaName
        Register(new ComponentDefinition(ComponentType.ScfComponent, "ScfComponent", "SCF", "$(ComponentsRootName)", "$(PrimaryName).meta.xml", IdentityStrategy.Name, IsFileBacked: true));

        // GenericComponent — probed identity, dynamic root directory
        Register(new ComponentDefinition(ComponentType.GenericComponent, "GenericComponent", "GenericComponent", "$(ComponentsRootName)", "$(PrimaryName).meta.xml", IdentityStrategy.Probed));

        // SolutionComponent
        Register(new ComponentDefinition(ComponentType.SolutionComponent, "SolutionComponent", "SolutionComponent", "Other", "Solution.xml", IdentityStrategy.Singleton));

        // Solution
        Register(new ComponentDefinition(ComponentType.Solution, "Solution", "SolutionManifest", "Other", "Solution.xml", IdentityStrategy.Singleton));
    }

    /// <summary>
    /// Looks up a component definition by its <see cref="ComponentType"/> code.
    /// </summary>
    public static ComponentDefinition? GetByType(ComponentType type) => _byType.GetValueOrDefault(type);

    /// <summary>
    /// Looks up a component definition by its XML element name (case-insensitive).
    /// </summary>
    public static ComponentDefinition? GetByXmlElement(string elementName) => _byXmlElement.GetValueOrDefault(elementName);

    /// <summary>
    /// Returns all registered component definitions.
    /// </summary>
    public static IEnumerable<ComponentDefinition> GetAll() => _byType.Values;

    private static void Register(ComponentDefinition def)
    {
        _byType[def.TypeCode] = def;
        _byXmlElement[def.XmlElementName] = def;
    }
}
