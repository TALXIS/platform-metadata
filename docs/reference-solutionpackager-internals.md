# SolutionPackager Library — Comprehensive Analysis

> Produced from decompiled source at `SolutionPackagerLib/`

---

## 1. Component Type Registry

### 1.1 ComponentType Enum (complete)

```csharp
// Microsoft.Crm.Tools.SolutionPackager/ComponentType.cs
public enum ComponentType
{
    Entity = 1,  Attribute = 2,  Relationship = 3,
    AttributePicklistValue = 4,  AttributeLookupValue = 5,  ViewAttribute = 6,
    LocalizedLabel = 7,  RelationshipExtraCondition = 8,
    OptionSet = 9,  EntityRelationship = 10,
    EntityRelationshipRole = 11,  EntityRelationshipRelationships = 12,
    ManagedProperty = 13,  EntityKey = 14,  EntityKeyAttribute = 15,
    Privilege = 16,  PrivilegeObjectTypeCode = 17,  EntityIndex = 18,
    Role = 20,  RolePrivileges = 21,
    DisplayString = 22,  DisplayStringMap = 23,
    Form = 24,  OrganizationSettings = 25,  SavedQuery = 26,
    Workflow = 29,  ProcessTrigger = 30,
    Report = 31,  ReportEntity = 32,  ReportCategory = 33,
    ReportVisibility = 34,  ActivityMimeAttachment = 35,
    Template = 36,  ContractTemplate = 37,  KbArticleTemplate = 38,
    MailMergeTemplate = 39,
    SyncAttributeMappingProfile = 41,
    EntityMap = 46,  AttributeMap = 47,
    RibbonCommand = 48,  RibbonContextGroup = 49,  RibbonCustomization = 50,
    RibbonRule = 52,  RibbonTabToCommandMap = 53,  RibbonDiff = 55,
    SavedQueryVisualization = 59,  SystemForm = 60,
    WebResource = 61,  SiteMap = 62,  ConnectionRole = 63,
    ComplexControl = 64,  ComplexControls = 64,   // alias
    HierarchyRule = 65,  CustomControl = 66,
    CustomControlDefaultConfig = 68,  CustomControlResource = 69,
    FieldSecurityProfile = 70,  FieldPermission = 71,
    SecuredMaskingRule = 72,  AttributeMaskingRule = 73,
    AppModule = 80,  AppModuleSiteMap = 81,
    AppModuleRibbonCommand = 82,  AppModuleToRoleMap = 88,
    PluginType = 90,  PluginAssembly = 91,
    SdkMessageProcessingStep = 92,  SdkMessageProcessingStepImage = 93,
    ServiceEndpoint = 95,  ServicePlans = 101,
    RoutingRule = 150,  RoutingRuleItem = 151,
    SLA = 152,  SLAItem = 153,
    ConvertRule = 154,  ConvertRuleItem = 155,
    KnowledgeBaseRecord = 156,
    ChannelPropertyGroup = 157,  ChannelProperty = 158,
    DependencyFeature = 160,
    MobileOfflineProfile = 161,  MobileOfflineProfileItem = 162,
    MobileOfflineProfileItemAssociation = 163,
    SimilarityRule = 167,  SimilarityRuleCondition = 168,
    ProfileRule = 169,  ProfileRuleItem = 170,
    ProfileEntityAccessLevel = 171,  ChannelAccessProfile = 172,
    RecommendationModel = 173,  RecommendationModelMapping = 174,
    KnowledgeSearchModel = 175,  TextAnalyticsEntityMapping = 176,
    TopicModelConfiguration = 177,  EmailSignature = 178,
    AdvancedSimilarityRule = 179,
    EntityDataSourceMapping = 180,  EntityDataProvider = 181,
    EntityDataSource = 183,
    AppConfig = 191,  AppConfigInstance = 192,
    EntityPrivilege = 200,
    SdkMessage = 201,  SdkMessageFilter = 202,  Maps = 202,  // alias
    SdkMessagePair = 203,
    SdkMessageRequest = 204,  SdkMessageRequestField = 205,
    SdkMessageResponse = 206,  SdkMessageResponseField = 207,
    ImportMap = 208,  StoredProcedure = 209,
    WebWizard = 210,  Dialogs = 211,
    ImportEntityMapping = 212,  ColumnMapping = 213,
    LookUpMapping = 214,  PickListMapping = 215,
    TransformationMapping = 216,  TransformationParameterMapping = 217,
    ImportData = 218,  ImportFile = 219,  ImportLog = 220,
    OwnerMapping = 221,  Dashboard = 222,
    NavigationSetting = 250,  NavigationSettingItem = 251,
    GlobalSearchConfiguration = 252,  CardType = 260,
    SolutionComponentDefinition = 270,
    CanvasApp = 300,  CanvasApps = 300,   // alias
    Connector = 371,  ECConnector = 372,
    EnvironmentVariableDefinition = 380,  EnvironmentVariableValue = 381,
    AITemplate = 400,  AIModel = 401,  AIConfiguration = 402,
    Bot = 403,
    Dataflow = 418,  DataflowEntities = 419,
    EntityAnalyticsConfiguration = 430,
    AttributeImageConfiguration = 431,  EntityImageConfiguration = 432,
    DualWriteEntityMap = 500,  DataIntegrationConnection = 501,
    ExportToDataLakeConfig = 510,  TeamTemplate = 511,
    InteractionCentricDashboard = 660,
    ServicePlanAppModules = 80001,
    ScfComponent = 99998,
    GenericComponent = 99999,
    SolutionComponent = 100000,
    Solution = 15062,
}
```

**Aliases**: `ComplexControls = ComplexControl = 64`, `CanvasApps = CanvasApp = 300`, `Maps = SdkMessageFilter = 202`.

### 1.2 Processor → ComponentType → XML Element Mapping

Each `[Export(typeof(IComponentProcessor))]` processor registers via its constructor:

| Processor Class | XML Element | ComponentType | Code |
|---|---|---|---|
| `EntityProcessor` | `Entities` | Entity | 1 |
| `OptionSetProcessor` | `optionsets` | OptionSet | 9 |
| `EntityRelationshipProcessor` | `EntityRelationships` | EntityRelationship | 10 |
| `RoleProcessor` | `Roles` | Role | 20 |
| `OrganizationSettingsProcessor` | `OrganizationSettings` | OrganizationSettings | 25 |
| `WorkflowProcessor` | `Workflows` | Workflow | 29 |
| `ReportProcessor` | `Reports` | Report | 31 |
| `TemplateProcessor` | `Templates` | Template | 36 |
| `SyncAttributeMappingProcessor` | `SyncAttributeMappingProfiles` | SyncAttr… | 41 |
| `EntityMapProcessor` | `EntityMaps` | EntityMap | 46 |
| `RibbonCustomizationProcessor` | `RibbonDiffXml` | RibbonCustomization | 50 |
| `WebResourceProcessor` | `WebResources` | WebResource | 61 |
| `SiteMapProcessor` | `SiteMap` | SiteMap | 62 |
| `ConnectionRoleProcessor` | `ConnectionRoles` | ConnectionRole | 63 |
| `CustomControlsProcessor` | `CustomControls` | CustomControl | 66 |
| `FieldSecurityProfileProcessor` | `FieldSecurityProfiles` | FieldSecurityProfile | 70 |
| `AppModuleProcessor` | `AppModules` | AppModule | 80 |
| `AppModuleSitemapProcessor` | `AppModuleSiteMaps` | AppModuleSiteMap | 81 |
| `PluginAssemblyProcessor` | `SolutionPluginAssemblies` | PluginAssembly | 91 |
| `SdkMessageProcessingStepProcessor` | `SdkMessageProcessingSteps` | SdkMsgProcStep | 92 |
| `ServiceEndPointProcessor` | `ServiceEndpoints` | ServiceEndpoint | 95 |
| `ServicePlanProcessor` | `serviceplans` | ServicePlans | 101 |
| `MobileOfflineProfileProcessor` | `MobileOfflineProfiles` | MobileOfflineProfile | 161 |
| `EntityPrivilegeProcessor` | `EntityPrivileges` | EntityPrivilege | 200 |
| `SdkMessageProcessor` | `SdkMessages` | SdkMessage | 201 |
| `ImportMapsProcessor` | `Maps` | SdkMessageFilter | 202 |
| `StoredProcedureProcessor` | `StoredProcedures` | StoredProcedure | 209 |
| `WebWizardProcessor` | `WebWizards` | WebWizard | 210 |
| `DialogProcessor` | `Dialogs` | Dialogs | 211 |
| `DashboardProcessor` | `Dashboards` | Dashboard | 222 |
| `ConnectorsProcessor` | `Connectors` | Connector | 371 |
| `EnvVariablesProcessor` | `EnvironmentVariables` | EnvVarDefinition | 380 |
| `CanvasAppsProcessor` | `CanvasApps` | CanvasApp | 300 |
| `TeamTemplateProcessor` | `TeamTemplates` | TeamTemplate | 511 |
| `InteractionCentricDashboardProcessor` | `InteractionCentricDashboards` | IC Dashboard | 660 |
| `ServicePlanAppModulesProcessor` | `serviceplanappmodulesset` | ServicePlanAppModules | 80001 |
| `ScfProcessor` | `SCF` | ScfComponent | 99998 |
| `GenericComponentProcessor` | *(dynamic)* | GenericComponent | 99999 |
| `SolutionComponentProcessor` | `SolutionComponent` | SolutionComponent | 100000 |
| `SolutionDataProcessor` | `SolutionManifest` | Solution | 15062 |

**Sub-processors** (instantiated by `TemplateProcessor`, not exported via MEF):
- `KbArticleTemplateProcessor` → `"KBArticleTemplates"`
- `EmailTemplateProcessor` → `"EmailTemplates"`
- `MailMergeTemplateProcessor` → `"MailMergeTemplates"`

### 1.3 Component Class Hierarchy

```
Component                     — Id (Guid?), PrimaryName, ComponentType, Element (XElement), ElementJson (JObject)
├── FileBackedComponent       — FileName, DiskFileName, SourceType (int?)
└── ScfBackedComponet         — ComponentName, SchemaName, Name
```

`ComponentCollection` wraps `Collection<Component>` with a `ComponentType`, `Element`/`ElementJson`, and `Name`.

---

## 2. File Layout / Path Mapping

### 2.1 Master Folder Mapping

From `ComponentConfigurationCollection` constructor — each line is `(ComponentType, Directory, FilePattern)`:

```csharp
// ComponentConfigurationCollection.cs (verbatim)
BaseAdd(new CCE(Entity,                     "Entities",                     "$(PrimaryName)/Entity.xml"));
BaseAdd(new CCE(OptionSet,                  "OptionSets",                   "$(PrimaryName)"));
BaseAdd(new CCE(EntityRelationship,         "Other",                        "Relationships.xml"));
BaseAdd(new CCE(SiteMap,                    "Other",                        "$(type)$(managed).xml"));
BaseAdd(new CCE(RibbonCustomization,        "Other",                        "$(type).xml"));
BaseAdd(new CCE(Role,                       "Roles",                        "$(PrimaryName)"));
BaseAdd(new CCE(ConnectionRole,             "Other",                        "$(type)s.xml"));
BaseAdd(new CCE(Dashboard,                  "Dashboards",                   "$(PrimaryName)"));
BaseAdd(new CCE(FieldSecurityProfile,       "Other",                        "$(type)s.xml"));
BaseAdd(new CCE(WebResource,                "WebResources",                 "$(PrimaryName)"));
BaseAdd(new CCE(Workflow,                   "Workflows",                    "Workflows.xml"));
BaseAdd(new CCE(PluginAssembly,             "PluginAssemblies",             "PluginAssemblies.xml"));
BaseAdd(new CCE(SdkMessageProcessingStep,   "SdkMessageProcessingSteps",    "$(PrimaryName)"));
BaseAdd(new CCE(ServiceEndpoint,            "PluginAssemblies",             "$(type)s.xml"));
BaseAdd(new CCE(Report,                     "Reports",                      "$(type)"));
BaseAdd(new CCE(Template,                   "Templates",                    "$(PrimaryName).xml"));
BaseAdd(new CCE(EntityMap,                  "Other",                        "$(type)s.xml"));
BaseAdd(new CCE(ProfileRule,                "ChannelAccess",                "ProfileRules/$(PrimaryName)"));
BaseAdd(new CCE(ChannelAccessProfile,       "ChannelAccess",                "Profiles/$(PrimaryName)"));
BaseAdd(new CCE(SdkMessage,                 "SdkMessages",                  "$(PrimaryName)"));
BaseAdd(new CCE(ComplexControl,             "ComplexControls",              "$(PrimaryName).xml"));
BaseAdd(new CCE(Dialogs,                    "Dialogs",                      "$(PrimaryName).xml"));
BaseAdd(new CCE(StoredProcedure,            "StoredProcedures",             "$(type)"));
BaseAdd(new CCE(AppModule,                  "AppModules",                   "$(PrimaryName)/AppModule$(managed).xml"));
BaseAdd(new CCE(AppModuleSiteMap,           "AppModuleSiteMaps",            "$(PrimaryName)/AppModuleSiteMap$(managed).xml"));
BaseAdd(new CCE(EntityPrivilege,            "EntityPrivileges",             "$(type)"));
BaseAdd(new CCE(WebWizard,                  "WebWizards",                   "$(PrimaryName).xml"));
BaseAdd(new CCE(SdkMessageFilter,           "Maps",                         "$(PrimaryName).xml"));
BaseAdd(new CCE(EntityDataProvider,         "EntityDataProviders",          "$(PrimaryName).xml"));
BaseAdd(new CCE(EntityDataSource,           "EntityDataSources",            "$(PrimaryName).xml"));
BaseAdd(new CCE(InteractionCentricDashboard,"InteractionCentricDashboards", "$(PrimaryName)"));
BaseAdd(new CCE(TeamTemplate,               "TeamTemplates",                "$(PrimaryName)"));
BaseAdd(new CCE(SyncAttributeMappingProfile,"SyncAttributeMappingProfiles", "$(PrimaryName)"));
BaseAdd(new CCE(MobileOfflineProfile,       "MobileOfflineProfiles",        "$(PrimaryName)"));
BaseAdd(new CCE(CustomControl,              "Controls",                     "$(PrimaryName)"));
BaseAdd(new CCE(EnvironmentVariableDefinition,"EnvironmentVariables",       "$(PrimaryName).xml"));
BaseAdd(new CCE(Connector,                  "$(ComponentsRootName)",        "$(PrimaryName).xml"));
BaseAdd(new CCE(OrganizationSettings,       "OrganizationSettings",         "_legacy/$(PrimaryName).meta.xml"));
BaseAdd(new CCE(CanvasApp,                  "CanvasApps",                   "$(PrimaryName).meta.xml"));
BaseAdd(new CCE(ServicePlans,               "ServicePlans",                 "ServicePlans.xml"));
BaseAdd(new CCE(ServicePlanAppModules,      "ServicePlans",                 "ServicePlanAppModules.xml"));
BaseAdd(new CCE(GenericComponent,           "$(ComponentsRootName)",        "$(PrimaryName).meta.xml"));
```

Plus two top-level files:
- `Other/Solution.xml`
- `Other/Customizations.xml`

### 2.2 Path Placeholder Resolution

From `ComponentProcessorBase.ResolvePathPlaceholder()`:

| Placeholder | Resolves To |
|---|---|
| `$(PrimaryName)` | Component's `PrimaryName` (sanitized via `CreateValidFileName`) |
| `$(type)` | `ComponentType.ToString()` (e.g. `"SiteMap"`, `"Report"`) |
| `$(managed)` | `"_managed"` if managed solution, `""` otherwise |
| `$(ComponentsRootName)` | The XML element name (for GenericComponent/Connector) |

Final path: `Path.Combine(context.RootFolder, resolvedDirectory, resolvedFilename)`

### 2.3 New Format (YAML) Path Transformation

When `IsNewFormat == true` (`ComponentConfigurationManager`):
- Directory names → **lowercased** (`.ToLowerInvariant()`)
- `.xml` → `.yml`, `.meta.xml` → `.yml`
- Solution: `solutions/{solutionName}/solution.yml`
- Publisher: `publishers/{publisherName}/publisher.yml`

### 2.4 Sharded Components

`ShardedComponents` handles binary/non-XML files from the solution ZIP that aren't managed by any processor. **Legacy directories** excluded from sharding:

```csharp
"Other", "Resources", "bin", "obj", "solutions", "publishers", "entityrelationships", "modernflows"
```

Plus any directories claimed by processors via `GetNonShardedComponentDirNames()`.

### 2.5 File Mapping / Remapping System

`Filer` reads an XML mapping file (`<Mapping>`) supporting 3 mapper types:
- `<Folder map="..." to="...">` → `BasicFolderMapper` — redirects entire folder trees
- `<FileToFile map="..." to="...">` → `FileToFileMapper` — single file redirect
- `<FileToPath map="..." to="...">` → `FileToPathMapper` — pattern-based file relocation

Supports `**` wildcard for recursive folder matching and `*` for filename extension matching.

### 2.6 Constants

```csharp
ManagedSolutionComponentFileSuffix = "_managed"
DefaultExtension = ".xml"
DefaultMetadataExtension = ".data.xml"
ResourcesFolder = "Resources"
RibbonDiffFileName = "RibbonDiff.xml"
StoredProcExtension = ".sql"
PluginFolderNameExternalFolderName = "PluginAssembliesExternal"
Other = "Other"
Solution = "Solution.xml"
Customizations = "Customizations.xml"
```

---

## 3. XML Splitting and Serialization

### 3.1 How `customizations.xml` Is Split

The core splitting logic is in `ZipReader.LoadCustomizations()`:

1. Loads `customizations.xml` as `XDocument`
2. Creates a **residual document** — a clone of the root with only attributes (no children)
3. Iterates each top-level child element:
   - Calls `context.GetComponentProcessor(element)` to find a processor by element name
   - If found → `processor.CreateComponents(element)` → adds to `context.Customizations.Components`
   - If not found or GenericComponent with no items → keeps element **verbatim** in residual XML
4. The residual XML becomes the on-disk `Other/Customizations.xml`

**Key insight**: Recognized components are extracted into separate folders/files. Unrecognized components remain in the residual customizations.xml.

### 3.2 XML Namespace Preservation

Critical utilities in `XmlExtensions.cs`:
- **`ElementsPreserveNamespace`**: Copies namespace declarations from parent → child when splitting
- **`CloneEmptyPreserveNamespace`**: Creates empty element preserving parent namespaces
- **`AddAndLiftNamespace`**: Re-merges by adding child and lifting its namespace declarations to parent

### 3.3 Write Modes (3 strategies controlled by flags)

| Flags | Behavior | Example |
|---|---|---|
| `isSingleComponentElement = true` | All → single file | SiteMap → `Other/SiteMap.xml` |
| `isWriteIndividualComponent = true` | Complex split with subfolders | Entity → `Entities/{Name}/Entity.xml` + subfolders |
| `isCollectionComponent = true` | One file per item in folder | Role → `Roles/{RoleName}.xml` |
| `isFileBackedComponent = true` | `.data.xml` metadata + binary file | WebResource → `WebResources/{Name}` + `.data.xml` |
| None of above | All → single file | Workflow → `Workflows/Workflows.xml` |

### 3.4 RESX Localization Flow

1. Each processor defines `locableElementXPaths` — XPath queries for localizable content
2. `DiskWriter.LocalizeComponents()` collects all `LocalizableElement`s
3. Groups by LCID → writes `Resources/{lcid}/resources.{lcid}.resx`
4. Optional template RESX at `Resources/template_resources.resx`

### 3.5 YAML/XML Dual Support

`XUtils.LoadElement`/`LoadDocument` transparently loads `.yml` files by converting YAML → XML first via `Helper.ConvertYamlToXml()`.

---

## 4. Merge vs. Overwrite Behavior

### 4.1 Default: Overwrite

Most processors call `Helper.WriteToFile(path, element)` which simply overwrites the target file.

### 4.2 Merge Cases

**a) `privilegeobjecttypecodes`** (in `ComponentProcessorBase.WriteCollectionToFolder`):
- Loads existing file, adds new element, writes combined result

**b) Nested multi-LCID** (`HandleDifferentLcidsWithSameId`):
- Preserves elements with different LCID values, replaces same-LCID elements

**c) Managed vs. Unmanaged (`PackageType == Both`):**
- `IsManaged && !IsDifferentInManaged` → **SKIPS writing** (reuses unmanaged)
- `IsDifferentInManaged == true` → writes with `_managed` suffix
- Specific processors with `IsDifferentInManaged = true`: EntityProcessor, SiteMapProcessor, AppModuleProcessor, AppModuleSitemapProcessor, SolutionDataProcessor

**d) `GetComponentPathPreferManagedIfAvailable`** (pack direction):
- First looks for `_managed` file, falls back to unmanaged if `UseUnmanagedFileForManaged` is set

### 4.3 Processor-Specific Merge Patterns

| Processor | Merge Behavior |
|---|---|
| **EntityProcessor** | Splits forms into `FormXml/{type}/`, saved queries into `SavedQueries/`, visualizations into `Visualizations/`. Each sub-element file can be independently merged |
| **EntityRelationshipProcessor** | Creates skeletal `Relationships.xml` + per-entity files in `Relationships/` subfolder. On read, merges skeletal + per-entity files |
| **EntityPrivilegeProcessor** | Splits `privileges` and `privilegeobjecttypecodeslist` into per-item files keyed by `PrivilegeId` |
| **AppModuleProcessor** | Splits `NavigationSettings` into subfolder. On read, reassembles. Sorts `AppModuleComponents` deterministically |
| **EnvVariablesProcessor** | Writes each definition as individual file. Separately writes `environment_variable_values.json` |
| **TemplateProcessor** | Delegates to sub-processors that write sub-elements (subject.xsl, body.xsl, attachments) as separate files |

### 4.4 Deterministic Sorting

`DiskWriter` sorts `RootComponents` and `MissingDependencies` by `type → schemaName → id` for deterministic output.

---

## 5. SCF (Solution Component Framework) Components

### 5.1 Overview

SCF components are the "new generation" platform extensibility mechanism (type code **99998**). They differ from legacy platform components in several key ways:

### 5.2 ScfProcessor

```csharp
[Export(typeof(IComponentProcessor))]
internal class ScfProcessor : ComponentProcessorBase
{
    public ScfProcessor() : base("SCF", ComponentType.ScfComponent)
    {
        isFileBackedComponent = true;
        isSingleComponentElement = true;
    }
}
```

- **XML element**: `"SCF"`
- **File-backed**: Each SCF component has associated binary/JSON files
- **Dual format**: Handles both JSON (`JObject`) and XML (`XElement`) inputs
- **No localization**: `GetLocalizableElements()` returns empty collection (SCF handles its own i18n)

### 5.3 Identity Resolution (differs from platform components)

```csharp
// For JSON (new format):
ComponentName = element.First.Path.Trim()       // e.g. "Bot", "AIModel"
Name          = GetJsonValue(element, "name")
SchemaName    = GetJsonValue(element, "@uniquename")

// For XML (legacy format):
ComponentName = element.Name.LocalName
Name          = GetElementValue(element, "name")
SchemaName    = from "schemaname" attr/element, fallback to "primarykey" attr/element
```

### 5.4 ScfBackedComponet (model class)

```csharp
internal class ScfBackedComponet : Component
{
    public string ComponentName { get; set; }  // e.g. "Bot", "AIModel"
    public string SchemaName { get; set; }     // unique name / primary key
    public string Name { get; set; }           // display name
}
```

### 5.5 ScfMetadata (serialized metadata)

```csharp
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class ScfMetadata
{
    public string ScfComponentTypeName { get; set; }  // e.g. "Bot"
    public string SchemaName { get; set; }
    public string Name { get; set; }
}
```

### 5.6 How SCF Differs From Platform Components

| Aspect | Platform Component | SCF Component |
|---|---|---|
| Type code | 1–660+ (specific) | 99998 (single code) |
| Identity | Type-specific (GUID, Name, composite) | `ComponentName` + `SchemaName` |
| Processor | Dedicated per type | Single `ScfProcessor` |
| Localization | RESX-based with XPath queries | None (self-managed) |
| File format | XML (`.xml`, `.data.xml`) | JSON or XML |
| Folder | Type-specific | Dynamic (by element name) |
| Examples | Entity, Workflow, WebResource | Bot, AIModel, custom SCF types |

### 5.7 SolutionMetadataComponents — SCF in Metadata

```csharp
public class SolutionMetadataComponents
{
    public IList<CopilotAgentSettings> CopilotAgents { get; set; }  // SCF where ComponentName=="Bot"
    public IList<CanvasAppMetadata> CanvasApps { get; set; }
    public IList<ConnectionReferenceMetadata> ConnectionReferences { get; set; }
    public IList<EnvironmentVariableMetadata> EnvironmentVariables { get; set; }
    public IList<WorkflowMetadata> Workflows { get; set; }
    public IList<ScfMetadata> ScfComponents { get; set; }
}
```

`SolutionMetadataFactory` builds this from inspection context. CopilotAgents are SCF components where `ComponentName == "Bot"`.

---

## 6. Identity and Deduplication

### 6.1 Identity Patterns by Processor

| Processor | ID Field | PrimaryName Field | Identity Strategy |
|---|---|---|---|
| **EntityProcessor** | *(none)* | `Name` element | Name-only (entity logical name) |
| **WorkflowProcessor** | `WorkflowId` attr (Guid) | `Name` attr | GUID + Name |
| **WebResourceProcessor** | `WebResourceId` element (Guid) | `Name` element | GUID + Name |
| **PluginAssemblyProcessor** | `PluginAssemblyId` attr (Guid) | `FullName` attr | GUID + FullName |
| **RoleProcessor** | `id` attr (Guid) | `name` attr | GUID + Name |
| **DashboardProcessor** | `FormId` element (Guid) | FormId as string | GUID as filename |
| **ReportProcessor** | `reportid` element (Guid) | `name` element | GUID + Name |
| **OptionSetProcessor** | *(none)* | `Name` attr | Name-only |
| **SiteMapProcessor** | *(none)* | `"SiteMap"` (hardcoded) | Singleton |
| **RibbonCustomizationProcessor** | *(none)* | `":RibbonDiffXml"` (hardcoded) | Singleton |
| **ConnectionRoleProcessor** | `connectionroleid` element (Guid) | *(none)* | GUID-only |
| **EntityRelationshipProcessor** | *(none)* | `Name` attr | Name-only, dedup check |
| **EntityMapProcessor** | *(none)* | `"{Source},{Target}"` | Composite key |
| **EntityPrivilegeProcessor** | *(none)* | *(none)* | Singleton-like |
| **SdkMessageProcessor** | `SdkMessageId` element (Guid) | `Name` element | GUID + Name |
| **SdkMessageProcessingStepProcessor** | `SdkMessageProcessingStepId` attr (Guid) | *(none)* | GUID-only |
| **FieldSecurityProfileProcessor** | `fieldsecurityprofileid` attr (Guid) | `name` attr | GUID + Name |
| **DialogProcessor** | `FormId` element (Guid) | FormId as string | GUID as filename |
| **CustomControlsProcessor** | *(none)* | `{namespace}.{constructor}` | Qualified name |
| **EnvVariablesProcessor** | *(none)* | `"EnvironmentVariables"` | Singleton wrapper |
| **ScfProcessor** | *(none)* | *(none)* | `ComponentName` + `SchemaName` |
| **GenericComponentProcessor** | *(none)* | `NameProber.SafeGetName()` | Probed name |

### 6.2 GenericComponentProcessor Name Probing

When no dedicated processor exists, `NameProber` tries these in order:

```csharp
static Func<XElement, string>[] _nameDiscoveryFuncs = {
    e => GetElementValue(e, "Name"),       // <Name>value</Name>
    e => GetAttributeValue(e, "Name"),     // Name="value"
    e => GetElementValue(e, "SchemaName"), // <SchemaName>value</SchemaName>
    e => GetElementValue(e, "name"),       // <name>value</name>
    e => GetAttributeValue(e, "name"),     // name="value"
    e => GetElementValue(e, "schemaName"), // <schemaName>value</schemaName>
};
```

If all fail and element has a value, generates: `"{elementName}-{SHA256(value)}"`.

### 6.3 GenericComponent Ignore List

These element names are silently skipped by `GenericComponentProcessor`:

```
solutioncomponentattributeconfigurations, solutioncomponentrelationshipconfiguration,
solutioncomponentconfigurations, serviceplans, bots, botcomponents, appelements
```

### 6.4 Deduplication Mechanisms

- **EntityRelationshipProcessor**: Explicitly checks for duplicate relationship names during read and throws `DuplicatedRelationshipName`
- **Customizations.AddComponentFile**: Prevents duplicate file entries (logs warning)
- **File-backed components**: The `.data.xml` ↔ binary file pairing prevents duplicates by filename uniqueness
- **Collection components**: Filename = GUID or PrimaryName ensures uniqueness within a folder
- **RootComponentInformation**: Components in the solution manifest are sorted deterministically by `ComponentTypeId → SchemaName → ComponentId`

### 6.5 Solution Filtering

All processors respect `context.SolutionInformation.SolutionComponentFiles` — a set of file paths that belong to the current solution. Components not in this set are excluded during read (pack) operations.

---

## 7. GenericProcessorFactory — Dynamic Dispatch

```csharp
[Export(typeof(IGenericProcessorFactory))]
internal class GenericProcessorFactory
{
    Dictionary<string, IComponentProcessor> namedProcessorMap;
    Lazy<HashSet<string>> knownComponents;  // built from all CCE.MainDirectory values

    IComponentProcessor GetProcessor(XElement element, bool haveName)
    {
        if (knownComponents.Value.Contains(element.Name.LocalName))
            return null;  // handled by dedicated processor
        if (!haveName && TryExtractName(element.FirstChild) == null)
            return null;  // can't identify — leave in residual
        return EnsureProcessor(element.Name.LocalName);
        // → creates GenericComponentProcessor(elementName) on demand
    }
}
```

---

## 8. IComponentProcessor Interface

```csharp
public interface IComponentProcessor
{
    string SupportedElementName { get; }
    ComponentType SupportedComponentType { get; }
    bool IsDifferentInManaged { get; }
    string ParentTagForNestedMultiLcids { get; set; }
    string ChildTagForNestedMultiLcids { get; set; }

    void Initialize(Context context);
    ComponentCollection CreateComponents(XElement element);     // extract
    ComponentCollection CreateComponents(JObject element);      // extract (JSON)
    ComponentCollection ReadFromFiles();                        // pack
    void WriteToFiles(ComponentCollection c, IEnumerable<string> ignore = null); // extract
    Collection<LocalizableElement> GetLocalizableElements(ComponentCollection c);
    HashSet<string> GetNonShardedComponentDirNames();
}
```

---

## 9. Summary: Typical Unpacked Solution Layout

```
<root>/
├── Other/
│   ├── Solution.xml                    # Solution manifest
│   ├── Customizations.xml              # Residual (unrecognized elements)
│   ├── Relationships.xml               # EntityRelationship
│   ├── SiteMap.xml / SiteMap_managed.xml
│   ├── RibbonCustomization.xml
│   ├── ConnectionRoles.xml
│   ├── FieldSecurityProfiles.xml
│   └── EntityMaps.xml
├── Entities/
│   └── {EntityName}/
│       ├── Entity.xml
│       ├── RibbonDiff.xml
│       ├── FormXml/{formType}/{formId}.xml
│       ├── SavedQueries/{queryId}.xml
│       └── Visualizations/{vizId}.xml
├── OptionSets/{name}.xml
├── Roles/{roleName}.xml
├── Workflows/Workflows.xml
├── WebResources/{name} + {name}.data.xml
├── PluginAssemblies/PluginAssemblies.xml + {name}.data.xml
├── Reports/{name} + {name}.data.xml
├── Dashboards/{formId}.xml
├── Templates/{type}.xml
├── Controls/{ns.ctor}/ + ControlManifest.xml
├── AppModules/{name}/AppModule.xml
├── AppModuleSiteMaps/{name}/AppModuleSiteMap.xml
├── EnvironmentVariables/{schemaName}.xml
├── CanvasApps/{name}.meta.xml
├── SdkMessages/{name}.xml
├── SdkMessageProcessingSteps/{name}.xml
├── Maps/{name}.xml
├── {GenericElementName}/{componentName}.meta.xml    # GenericComponent
├── Resources/
│   ├── {lcid}/resources.{lcid}.resx
│   └── template_resources.resx
└── {sharded-dirs}/                      # Binary files from ZIP
```
