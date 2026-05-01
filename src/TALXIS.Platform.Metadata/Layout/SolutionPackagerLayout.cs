namespace TALXIS.Platform.Metadata.Layout;

/// <summary>
/// Constants for well-known paths in a SolutionPackager workspace.
/// Values match the decompiled <c>ComponentConfigurationCollection</c> and related constants.
/// </summary>
public static class SolutionPackagerLayout
{
    public const string SolutionXmlPath = "Other/Solution.xml";
    public const string CustomizationsXmlPath = "Other/Customizations.xml";
    public const string RelationshipsXmlPath = "Other/Relationships.xml";

    public const string EntitiesDirectory = "Entities";
    public const string OptionSetsDirectory = "OptionSets";
    public const string RolesDirectory = "Roles";
    public const string WorkflowsDirectory = "Workflows";
    public const string WebResourcesDirectory = "WebResources";
    public const string ReportsDirectory = "Reports";
    public const string TemplatesDirectory = "Templates";
    public const string DashboardsDirectory = "Dashboards";
    public const string PluginAssembliesDirectory = "PluginAssemblies";
    public const string PluginAssembliesExternalDirectory = "PluginAssembliesExternal";
    public const string SdkMessageProcessingStepsDirectory = "SdkMessageProcessingSteps";
    public const string SdkMessagesDirectory = "SdkMessages";
    public const string AppModulesDirectory = "AppModules";
    public const string AppModuleSiteMapsDirectory = "AppModuleSiteMaps";
    public const string ControlsDirectory = "Controls";
    public const string ComplexControlsDirectory = "ComplexControls";
    public const string CanvasAppsDirectory = "CanvasApps";
    public const string EnvironmentVariablesDirectory = "EnvironmentVariables";
    public const string ServicePlansDirectory = "ServicePlans";
    public const string MapsDirectory = "Maps";
    public const string DialogsDirectory = "Dialogs";
    public const string StoredProceduresDirectory = "StoredProcedures";
    public const string WebWizardsDirectory = "WebWizards";
    public const string EntityDataProvidersDirectory = "EntityDataProviders";
    public const string EntityDataSourcesDirectory = "EntityDataSources";
    public const string EntityPrivilegesDirectory = "EntityPrivileges";
    public const string InteractionCentricDashboardsDirectory = "InteractionCentricDashboards";
    public const string TeamTemplatesDirectory = "TeamTemplates";
    public const string SyncAttributeMappingProfilesDirectory = "SyncAttributeMappingProfiles";
    public const string MobileOfflineProfilesDirectory = "MobileOfflineProfiles";
    public const string ChannelAccessDirectory = "ChannelAccess";
    public const string OrganizationSettingsDirectory = "OrganizationSettings";
    public const string OtherDirectory = "Other";
    public const string ResourcesDirectory = "Resources";

    public const string ManagedSolutionComponentFileSuffix = "_managed";
    public const string DefaultExtension = ".xml";
    public const string DefaultMetadataExtension = ".data.xml";
    public const string RibbonDiffFileName = "RibbonDiff.xml";
    public const string StoredProcExtension = ".sql";
}
