namespace TALXIS.Platform.Metadata;

/// <summary>
/// All component type codes from SolutionPackager.
/// Values match the decompiled <c>Microsoft.Crm.Tools.SolutionPackager.ComponentType</c> enum.
/// </summary>
public enum ComponentType
{
    /// <summary>Table / entity definition.</summary>
    Entity = 1,

    /// <summary>Column / attribute definition.</summary>
    Attribute = 2,

    Relationship = 3,
    AttributePicklistValue = 4,
    AttributeLookupValue = 5,
    ViewAttribute = 6,
    LocalizedLabel = 7,
    RelationshipExtraCondition = 8,

    /// <summary>Global or local option set.</summary>
    OptionSet = 9,

    EntityRelationship = 10,
    EntityRelationshipRole = 11,
    EntityRelationshipRelationships = 12,
    ManagedProperty = 13,

    /// <summary>Alternate key definition.</summary>
    EntityKey = 14,

    EntityKeyAttribute = 15,
    Privilege = 16,
    PrivilegeObjectTypeCode = 17,
    EntityIndex = 18,

    /// <summary>Security role.</summary>
    Role = 20,

    RolePrivileges = 21,
    DisplayString = 22,
    DisplayStringMap = 23,

    /// <summary>Legacy form (type 24, distinct from SystemForm = 60).</summary>
    Form = 24,

    OrganizationSettings = 25,

    /// <summary>Saved query / view.</summary>
    SavedQuery = 26,

    /// <summary>Classic workflow / action / business rule.</summary>
    Workflow = 29,

    ProcessTrigger = 30,
    Report = 31,
    ReportEntity = 32,
    ReportCategory = 33,
    ReportVisibility = 34,
    ActivityMimeAttachment = 35,
    Template = 36,
    ContractTemplate = 37,
    KbArticleTemplate = 38,
    MailMergeTemplate = 39,
    SyncAttributeMappingProfile = 41,
    EntityMap = 46,
    AttributeMap = 47,
    RibbonCommand = 48,
    RibbonContextGroup = 49,
    RibbonCustomization = 50,
    RibbonRule = 52,
    RibbonTabToCommandMap = 53,
    RibbonDiff = 55,
    SavedQueryVisualization = 59,

    /// <summary>Model-driven app form (main, quick-create, quick-view, card, etc.).</summary>
    SystemForm = 60,

    /// <summary>Web resource (HTML, JS, CSS, image, etc.).</summary>
    WebResource = 61,

    /// <summary>Application site map.</summary>
    SiteMap = 62,

    ConnectionRole = 63,
    ComplexControl = 64,
    HierarchyRule = 65,
    CustomControl = 66,
    CustomControlDefaultConfig = 68,
    CustomControlResource = 69,
    FieldSecurityProfile = 70,
    FieldPermission = 71,
    SecuredMaskingRule = 72,
    AttributeMaskingRule = 73,

    /// <summary>Model-driven app module.</summary>
    AppModule = 80,

    AppModuleSiteMap = 81,
    AppModuleRibbonCommand = 82,
    AppModuleToRoleMap = 88,
    PluginType = 90,

    /// <summary>Plugin assembly (DLL).</summary>
    PluginAssembly = 91,

    /// <summary>SDK message processing step (plugin step registration).</summary>
    SdkMessageProcessingStep = 92,

    SdkMessageProcessingStepImage = 93,
    ServiceEndpoint = 95,
    ServicePlans = 101,
    RoutingRule = 150,
    RoutingRuleItem = 151,
    SLA = 152,
    SLAItem = 153,
    ConvertRule = 154,
    ConvertRuleItem = 155,
    KnowledgeBaseRecord = 156,
    ChannelPropertyGroup = 157,
    ChannelProperty = 158,
    DependencyFeature = 160,
    MobileOfflineProfile = 161,
    MobileOfflineProfileItem = 162,
    MobileOfflineProfileItemAssociation = 163,
    SimilarityRule = 167,
    SimilarityRuleCondition = 168,
    ProfileRule = 169,
    ProfileRuleItem = 170,
    ProfileEntityAccessLevel = 171,
    ChannelAccessProfile = 172,
    RecommendationModel = 173,
    RecommendationModelMapping = 174,
    KnowledgeSearchModel = 175,
    TextAnalyticsEntityMapping = 176,
    TopicModelConfiguration = 177,
    EmailSignature = 178,
    AdvancedSimilarityRule = 179,
    EntityDataSourceMapping = 180,
    EntityDataProvider = 181,
    EntityDataSource = 183,
    AppConfig = 191,
    AppConfigInstance = 192,
    EntityPrivilege = 200,
    SdkMessage = 201,
    SdkMessageFilter = 202,
    SdkMessagePair = 203,
    SdkMessageRequest = 204,
    SdkMessageRequestField = 205,
    SdkMessageResponse = 206,
    SdkMessageResponseField = 207,
    ImportMap = 208,
    StoredProcedure = 209,
    WebWizard = 210,
    Dialogs = 211,
    ImportEntityMapping = 212,
    ColumnMapping = 213,
    LookUpMapping = 214,
    PickListMapping = 215,
    TransformationMapping = 216,
    TransformationParameterMapping = 217,
    ImportData = 218,
    ImportFile = 219,
    ImportLog = 220,
    OwnerMapping = 221,
    Dashboard = 222,
    NavigationSetting = 250,
    NavigationSettingItem = 251,
    GlobalSearchConfiguration = 252,
    CardType = 260,
    SolutionComponentDefinition = 270,

    /// <summary>Canvas app.</summary>
    CanvasApp = 300,

    Connector = 371,
    ECConnector = 372,
    EnvironmentVariableDefinition = 380,
    EnvironmentVariableValue = 381,
    AITemplate = 400,
    AIModel = 401,
    AIConfiguration = 402,
    Bot = 403,
    Dataflow = 418,
    DataflowEntities = 419,
    EntityAnalyticsConfiguration = 430,
    AttributeImageConfiguration = 431,
    EntityImageConfiguration = 432,
    DualWriteEntityMap = 500,
    DataIntegrationConnection = 501,
    ExportToDataLakeConfig = 510,
    TeamTemplate = 511,
    InteractionCentricDashboard = 660,
    ServicePlanAppModules = 80001,

    /// <summary>Solution Component Framework component (new-generation extensibility).</summary>
    ScfComponent = 99998,

    /// <summary>Generic / unrecognized component handled by name probing.</summary>
    GenericComponent = 99999,

    SolutionComponent = 100000,
    Solution = 15062,
}
