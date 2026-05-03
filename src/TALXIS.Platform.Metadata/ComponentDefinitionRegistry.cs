namespace TALXIS.Platform.Metadata;

/// <summary>
/// Static registry of all known component definitions, pre-populated from SolutionPackager internals.
/// Cross-references Section 1.2 (Processor → serialized name), Section 2.1 (file layout),
/// and Section 6.1 (identity strategies).
/// </summary>
public static class ComponentDefinitionRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<ComponentType, ComponentDefinition> _byType = new();
    private static readonly Dictionary<string, ComponentDefinition> _bySerializedName = new(StringComparer.OrdinalIgnoreCase);

    static ComponentDefinitionRegistry()
    {
        // Entity — Name-only identity, merge support, subfolders for forms/views/etc.
        Register(new ComponentDefinition(ComponentType.Entity, "Entity", "Entities", "Entities", "$(PrimaryName)/Entity.xml", IdentityStrategy.Name,
            SupportsMerge: true, HasSubfolders: true,
            IsMergeable: false, HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "entityid", SerializedPath: "/ImportExportXml/Entities/Entity"));

        // Attribute — child of Entity
        Register(new ComponentDefinition(ComponentType.Attribute, "Attribute", "Attributes", "Entities", "$(PrimaryName)/Attributes.xml", IdentityStrategy.Name,
            HasParent: true, RootComponent: 1,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "attributeid", GroupParentComponentType: 1, GroupParentComponentAttributeName: "entityid"));

        // OptionSet — Name-only identity
        Register(new ComponentDefinition(ComponentType.OptionSet, "OptionSet", "optionsets", "OptionSets", "$(PrimaryName)", IdentityStrategy.Name,
            IsMergeable: false, HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "optionsetid"));

        // EntityRelationship — Name-only identity, single shared file
        Register(new ComponentDefinition(ComponentType.EntityRelationship, "EntityRelationship", "EntityRelationships", "Other", "Relationships.xml", IdentityStrategy.Name));

        // Form (legacy type 24) — child of Entity, mergeable
        Register(new ComponentDefinition(ComponentType.Form, "Form", "Forms", "Entities", "$(PrimaryName)/Forms.xml", IdentityStrategy.Guid,
            IsMergeable: true, HasParent: true, RootComponent: 1,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "formid", GroupParentComponentType: 1, GroupParentComponentAttributeName: "objecttypecode"));

        // SavedQuery — child of Entity
        Register(new ComponentDefinition(ComponentType.SavedQuery, "SavedQuery", "SavedQueries", "Entities", "$(PrimaryName)/SavedQueries.xml", IdentityStrategy.Guid,
            HasParent: true, RootComponent: 1,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "savedqueryid", GroupParentComponentType: 1, GroupParentComponentAttributeName: "returnedtypecode"));

        // SiteMap — Singleton identity, managed variant support, mergeable
        Register(new ComponentDefinition(ComponentType.SiteMap, "SiteMap", "SiteMap", "Other", "$(type)$(managed).xml", IdentityStrategy.Singleton,
            IsMergeable: true, HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true));

        // RibbonCustomization — Singleton identity
        Register(new ComponentDefinition(ComponentType.RibbonCustomization, "RibbonCustomization", "RibbonDiffXml", "Other", "$(type).xml", IdentityStrategy.Singleton));

        // Role — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.Role, "Role", "Roles", "Roles", "$(PrimaryName)", IdentityStrategy.Guid,
            IsMergeable: false, HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "roleid"));

        // ConnectionRole — GUID-only identity
        Register(new ComponentDefinition(ComponentType.ConnectionRole, "ConnectionRole", "ConnectionRoles", "Other", "$(type)s.xml", IdentityStrategy.Guid));

        // Dashboard — GUID as filename
        Register(new ComponentDefinition(ComponentType.Dashboard, "Dashboard", "Dashboards", "Dashboards", "$(PrimaryName)", IdentityStrategy.Guid));

        // FieldSecurityProfile — GUID + Name identity
        Register(new ComponentDefinition(ComponentType.FieldSecurityProfile, "FieldSecurityProfile", "FieldSecurityProfiles", "Other", "$(type)s.xml", IdentityStrategy.Guid));

        // SystemForm (type 60) — child of Entity, mergeable
        Register(new ComponentDefinition(ComponentType.SystemForm, "SystemForm", "SystemForms", "Entities", "$(PrimaryName)/FormXml", IdentityStrategy.Guid,
            SupportsMerge: true,
            IsMergeable: true, HasParent: true, RootComponent: 1,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "formid", GroupParentComponentType: 1, GroupParentComponentAttributeName: "objecttypecode"));

        // WebResource — GUID + Name identity, file-backed
        Register(new ComponentDefinition(ComponentType.WebResource, "WebResource", "WebResources", "WebResources", "$(PrimaryName)", IdentityStrategy.Guid,
            IsFileBacked: true,
            HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "webresourceid"));

        // Workflow — GUID + Name identity, single collection file
        Register(new ComponentDefinition(ComponentType.Workflow, "Workflow", "Workflows", "Workflows", "Workflows.xml", IdentityStrategy.Guid,
            HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "workflowid"));

        // PluginAssembly — GUID + FullName identity
        Register(new ComponentDefinition(ComponentType.PluginAssembly, "PluginAssembly", "SolutionPluginAssemblies", "PluginAssemblies", "PluginAssemblies.xml", IdentityStrategy.Guid,
            HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, CanBeDeleted: true,
            PrimaryKeyName: "pluginassemblyid"));

        // SdkMessageProcessingStep — GUID-only identity, child of PluginAssembly
        Register(new ComponentDefinition(ComponentType.SdkMessageProcessingStep, "SdkMessageProcessingStep", "SdkMessageProcessingSteps", "SdkMessageProcessingSteps", "$(PrimaryName)", IdentityStrategy.Guid,
            HasParent: true, RootComponent: 91,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "sdkmessageprocessingstepid"));

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
        Register(new ComponentDefinition(ComponentType.AppModule, "AppModule", "AppModules", "AppModules", "$(PrimaryName)/AppModule$(managed).xml", IdentityStrategy.Guid,
            SupportsMerge: true, HasSubfolders: true,
            IsMergeable: false, HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, IsCustomizable: true, CanBeDeleted: true,
            PrimaryKeyName: "appmoduleid"));

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
        Register(new ComponentDefinition(ComponentType.CustomControl, "CustomControl", "CustomControls", "Controls", "$(PrimaryName)", IdentityStrategy.Name,
            HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, CanBeDeleted: true,
            PrimaryKeyName: "customcontrolid"));

        // EnvironmentVariableDefinition — Singleton wrapper
        Register(new ComponentDefinition(ComponentType.EnvironmentVariableDefinition, "EnvironmentVariableDefinition", "EnvironmentVariables", "EnvironmentVariables", "$(PrimaryName).xml", IdentityStrategy.Singleton));

        // Connector — dynamic root directory
        Register(new ComponentDefinition(ComponentType.Connector, "Connector", "Connectors", "$(ComponentsRootName)", "$(PrimaryName).xml", IdentityStrategy.Name,
            HasParent: false, RootComponent: 0,
            AllowOverwriteCustomizations: true, CanBeDeleted: true,
            PrimaryKeyName: "connectorid"));

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
    public static ComponentDefinition? GetByType(ComponentType type)
    {
        lock (SyncRoot)
        {
            return _byType.TryGetValue(type, out var def) ? def : null;
        }
    }

    /// <summary>
    /// Looks up a component definition by its serialized name (case-insensitive).
    /// </summary>
    public static ComponentDefinition? GetBySerializedName(string serializedName)
    {
        lock (SyncRoot)
        {
            return _bySerializedName.TryGetValue(serializedName, out var def) ? def : null;
        }
    }

    /// <summary>
    /// Returns all registered component definitions.
    /// </summary>
    public static IEnumerable<ComponentDefinition> GetAll()
    {
        lock (SyncRoot)
        {
            return _byType.Values.ToArray();
        }
    }

    public static void Register(ComponentDefinition def, bool replaceExisting = false)
    {
        if (string.IsNullOrWhiteSpace(def.Name))
            throw new ArgumentException("Component definition name cannot be null or whitespace.", nameof(def));

        if (string.IsNullOrWhiteSpace(def.SerializedName))
            throw new ArgumentException("Component definition serialized name cannot be null or whitespace.", nameof(def));

        if (string.IsNullOrWhiteSpace(def.Directory))
            throw new ArgumentException("Component definition directory cannot be null or whitespace.", nameof(def));

        if (string.IsNullOrWhiteSpace(def.FilePattern))
            throw new ArgumentException("Component definition file pattern cannot be null or whitespace.", nameof(def));

        lock (SyncRoot)
        {
            if (!replaceExisting)
            {
                if (_byType.ContainsKey(def.TypeCode))
                    throw new InvalidOperationException($"A component definition for type '{def.TypeCode}' is already registered.");

                if (_bySerializedName.TryGetValue(def.SerializedName, out var existing))
                    throw new InvalidOperationException(
                        $"A component definition with serialized name '{def.SerializedName}' is already registered for type '{existing.TypeCode}'.");
            }
            else
            {
                if (_byType.TryGetValue(def.TypeCode, out var existingByType))
                    _bySerializedName.Remove(existingByType.SerializedName);

                if (_bySerializedName.TryGetValue(def.SerializedName, out var existingByName))
                    _byType.Remove(existingByName.TypeCode);
            }

            _byType[def.TypeCode] = def;
            _bySerializedName[def.SerializedName] = def;
        }
    }
}
