using System.Xml;
using System.Xml.Linq;
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Loads a SolutionPackager workspace directory into the core metadata model.
/// </summary>
public sealed class XmlWorkspaceReader
{
    private const LoadOptions XmlLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    /// <summary>
    /// Loads a solution workspace from disk.
    /// </summary>
    /// <param name="workspacePath">Root directory of the solution project (contains Other/Solution.xml).</param>
    public Workspace Load(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
            throw new DirectoryNotFoundException($"Workspace directory not found: {workspacePath}");

        var workspace = new Workspace(workspacePath);

        LoadSolution(workspace, workspacePath);
        LoadEntities(workspace, workspacePath);
        LoadGlobalOptionSets(workspace, workspacePath);
        LoadRelationships(workspace, workspacePath);
        LoadForms(workspace, workspacePath);
        LoadViews(workspace, workspacePath);
        LoadWebResources(workspace, workspacePath);
        LoadWorkflows(workspace, workspacePath);
        LoadFlowDefinitions(workspace, workspacePath);
        LoadPluginAssemblies(workspace, workspacePath);
        LoadSdkMessageProcessingSteps(workspace, workspacePath);
        LoadSecurityRoles(workspace, workspacePath);
        LoadAppModules(workspace, workspacePath);
        LoadSiteMaps(workspace, workspacePath);
        LoadRibbons(workspace, workspacePath);
        LoadRoundtripPassthroughFiles(workspace, workspacePath);
        LoadGenericComponents(workspace, workspacePath);

        return workspace;
    }

    private static void LoadSolution(Workspace workspace, string rootPath)
    {
        var solutionFile = Path.Combine(rootPath, "Other", "Solution.xml");
        if (!File.Exists(solutionFile)) return;

        var doc = LoadDocument(solutionFile);
        workspace.OriginalDocuments["Solution.xml"] = doc;
        var manifest = doc.Root?.Element("SolutionManifest");
        if (manifest == null) return;

        var solution = new Solution
        {
            UniqueName = manifest.Element("UniqueName")?.Value ?? "Unknown",
            Version = manifest.Element("Version")?.Value ?? "1.0.0.0",
            ManagedValue = manifest.Element("Managed")?.Value ?? "0",
            Source = CreateSourceLocation(solutionFile, manifest)
        };

        // Display name (all languages)
        var displayLabel = ReadLabel(manifest.Element("LocalizedNames"), "LocalizedName");
        if (displayLabel != null) solution.DisplayName = displayLabel;

        // Publisher
        var pubEl = manifest.Element("Publisher");
        if (pubEl != null)
        {
            var publisher = new Publisher
            {
                UniqueName = pubEl.Element("UniqueName")?.Value ?? "Unknown",
                Prefix = pubEl.Element("CustomizationPrefix")?.Value ?? "new",
                Source = CreateSourceLocation(solutionFile, pubEl)
            };

            var pubLabel = ReadLabel(pubEl.Element("LocalizedNames"), "LocalizedName");
            if (pubLabel != null) publisher.DisplayName = pubLabel;

            var optValPrefix = pubEl.Element("CustomizationOptionValuePrefix")?.Value;
            if (int.TryParse(optValPrefix, out var ovp))
                publisher.OptionValuePrefix = ovp;

            solution.Publisher = publisher;
        }

        // Root components
        var rootComponents = manifest.Element("RootComponents");
        if (rootComponents != null)
        {
            foreach (var rc in rootComponents.Elements("RootComponent"))
            {
                var typeStr = rc.Attribute("type")?.Value;
                if (!int.TryParse(typeStr, out var typeCode)) continue;

                var component = new RootComponent
                {
                    TypeCode = typeCode,
                    SchemaName = rc.Attribute("schemaName")?.Value,
                    Behavior = ParseInt(rc.Attribute("behavior")?.Value, 0)
                };

                var idStr = rc.Attribute("id")?.Value;
                if (Guid.TryParse(idStr, out var id))
                    component.Id = id;

                solution.AddRootComponent(component);
            }
        }

        workspace.Solution = solution;
    }

    private static void LoadEntities(Workspace workspace, string rootPath)
    {
        var entitiesDir = Path.Combine(rootPath, "Entities");
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityDir in Directory.GetDirectories(entitiesDir))
        {
            var entityFile = Path.Combine(entityDir, "Entity.xml");
            if (!File.Exists(entityFile)) continue;

            var (entity, doc) = ParseEntityFile(entityFile);
            if (entity != null && doc != null)
            {
                workspace.AddEntity(entity);
                workspace.OriginalDocuments[$"Entity:{entity.LogicalName}"] = doc;
            }
        }
    }

    private static (EntityMetadata? entity, XDocument? doc) ParseEntityFile(string filePath)
    {
        var doc = LoadDocument(filePath);
        var root = doc.Root; // <Entity>
        if (root == null) return (null, null);

        var entityInfo = root.Element("EntityInfo")?.Element("entity");
        if (entityInfo == null) return (null, null);

        var logicalName = entityInfo.Attribute("Name")?.Value
            ?? root.Element("Name")?.Value;
        if (string.IsNullOrEmpty(logicalName)) return (null, null);

        var entity = new EntityMetadata
        {
            LogicalName = logicalName,
            SchemaName = logicalName,
            Source = CreateSourceLocation(filePath, entityInfo)
        };

        // Display name from <Name LocalizedName="...">
        var nameEl = root.Element("Name");
        var localizedNameAttr = nameEl?.Attribute("LocalizedName")?.Value;
        if (localizedNameAttr != null)
            entity.DisplayName = new Label(localizedNameAttr);

        // Localized names from EntityInfo (all languages, overrides if present)
        var entityDisplayLabel = ReadLabel(entityInfo.Element("LocalizedNames"), "LocalizedName");
        if (entityDisplayLabel != null) entity.DisplayName = entityDisplayLabel;

        // Plural name
        var pluralLabel = ReadLabel(entityInfo.Element("LocalizedCollectionNames"), "LocalizedCollectionName");
        if (pluralLabel != null) entity.PluralName = pluralLabel;

        // Description
        var entityDescLabel = ReadLabel(entityInfo.Element("Descriptions"), "Description");
        if (entityDescLabel != null) entity.Description = entityDescLabel;

        // Entity-level properties
        entity.EntitySetName = entityInfo.Element("EntitySetName")?.Value;
        entity.IsActivity = entityInfo.Element("IsActivity")?.Value == "1";
        entity.IsAuditEnabled = entityInfo.Element("IsAuditEnabled")?.Value == "1";
        entity.ChangeTrackingEnabled = entityInfo.Element("ChangeTrackingEnabled")?.Value == "1";
        entity.IsMSTeamsIntegrationEnabled = entityInfo.Element("IsMSTeamsIntegrationEnabled")?.Value == "1";
        entity.ExternalName = entityInfo.Element("ExternalName")?.Value;
        entity.ExternalCollectionName = entityInfo.Element("ExternalCollectionName")?.Value;
        entity.EntityColor = entityInfo.Element("EntityColor")?.Value;

        var ownership = entityInfo.Element("OwnershipTypeMask")?.Value;
        entity.Ownership = ownership switch
        {
            "UserOwned" => OwnershipType.UserOwned,
            "OrganizationOwned" => OwnershipType.OrganizationOwned,
            "None" => OwnershipType.None,
            _ => OwnershipType.UserOwned
        };

        // Determine IsCustomEntity from entity name convention (has publisher prefix)
        entity.IsCustomEntity = logicalName.Contains('_');

        // Parse attributes
        var attributes = entityInfo.Element("attributes");
        if (attributes != null)
        {
            foreach (var attrEl in attributes.Elements("attribute"))
            {
                var attr = ParseAttribute(attrEl, filePath);
                if (attr != null)
                {
                    entity.AddAttribute(attr);

                    // Detect primary key and primary name
                    var displayMask = attrEl.Element("DisplayMask")?.Value ?? "";
                    var type = attrEl.Element("Type")?.Value?.ToLowerInvariant();
                    if (type == "primarykey")
                        entity.PrimaryIdAttribute = attr.LogicalName;
                    if (displayMask.Contains("PrimaryName"))
                        entity.PrimaryNameAttribute = attr.LogicalName;
                }
            }
        }

        return (entity, doc);
    }

    private static AttributeMetadata? ParseAttribute(XElement attrEl, string filePath)
    {
        var typeStr = attrEl.Element("Type")?.Value?.ToLowerInvariant() ?? "";
        var logicalName = attrEl.Element("LogicalName")?.Value;
        if (string.IsNullOrEmpty(logicalName)) return null;

        var attr = CreateTypedAttribute(typeStr, attrEl, logicalName);
        if (attr == null) return null;
        attr.SchemaName = attrEl.Attribute("PhysicalName")?.Value ?? logicalName;
        attr.IsCustomAttribute = attrEl.Element("IsCustomField")?.Value == "1";
        attr.IsAuditEnabled = attrEl.Element("IsAuditEnabled")?.Value == "1";
        attr.IsSecured = attrEl.Element("IsSecured")?.Value == "1";
        attr.IsSearchable = attrEl.Element("IsSearchable")?.Value == "1";
        attr.Source = CreateSourceLocation(filePath, attrEl);

        // Required level
        attr.RequiredLevel = RequiredLevelXml.Parse(attrEl.Element("RequiredLevel")?.Value);

        // Display name
        var attrDisplayLabel = ReadLabel(attrEl.Element("displaynames"), "displayname");
        if (attrDisplayLabel != null) attr.DisplayName = attrDisplayLabel;

        // Description
        var attrDescLabel = ReadLabel(attrEl.Element("Descriptions"), "Description");
        if (attrDescLabel != null) attr.Description = attrDescLabel;

        return attr;
    }

    private static AttributeMetadata? CreateTypedAttribute(string typeStr, XElement attrEl, string logicalName)
    {
        switch (typeStr)
        {
            case "nvarchar" or "ntext":
            {
                var sa = new StringAttributeMetadata { LogicalName = logicalName };
                if (int.TryParse(attrEl.Element("MaxLength")?.Value ?? attrEl.Element("Length")?.Value, out var maxLen))
                    sa.MaxLength = maxLen;
                var fmt = attrEl.Element("Format")?.Value?.ToLowerInvariant();
                sa.FormatName = fmt switch
                {
                    "email" => StringFormatName.Email,
                    "url" => StringFormatName.Url,
                    "phone" => StringFormatName.Phone,
                    "textarea" => StringFormatName.TextArea,
                    "tickersymbol" => StringFormatName.TickerSymbol,
                    _ => StringFormatName.Text
                };
                return sa;
            }
            case "primarykey":
                return new UniqueIdentifierAttributeMetadata { LogicalName = logicalName };

            case "lookup" or "customer" or "owner":
            {
                if (typeStr == "owner")
                    return new LookupAttributeMetadata { LogicalName = logicalName }; // Owner type maps to Lookup in model
                return new LookupAttributeMetadata { LogicalName = logicalName };
            }
            case "datetime":
            {
                var da = new DateTimeAttributeMetadata { LogicalName = logicalName };
                var fmt = attrEl.Element("Format")?.Value?.ToLowerInvariant();
                da.Format = fmt switch
                {
                    "date" or "dateonly" => DateTimeFormat.DateOnly,
                    _ => DateTimeFormat.DateAndTime
                };
                var behavior = attrEl.Element("Behavior")?.Value;
                da.DateTimeBehavior = behavior switch
                {
                    "0" or "3" => DateTimeBehavior.UserLocal,
                    "1" => DateTimeBehavior.DateOnly,
                    "2" => DateTimeBehavior.TimeZoneIndependent,
                    _ => DateTimeBehavior.UserLocal
                };
                return da;
            }
            case "int" or "integer":
            {
                var ia = new IntegerAttributeMetadata { LogicalName = logicalName };
                if (int.TryParse(attrEl.Element("MinValue")?.Value, out var min))
                    ia.MinValue = min;
                if (int.TryParse(attrEl.Element("MaxValue")?.Value, out var max))
                    ia.MaxValue = max;
                var fmt = attrEl.Element("Format")?.Value?.ToLowerInvariant();
                ia.Format = fmt switch
                {
                    "duration" => IntegerFormat.Duration,
                    "timezone" => IntegerFormat.TimeZone,
                    "language" => IntegerFormat.Language,
                    "locale" => IntegerFormat.Locale,
                    _ => IntegerFormat.None
                };
                return ia;
            }
            case "decimal":
            {
                var da = new DecimalAttributeMetadata { LogicalName = logicalName };
                if (decimal.TryParse(attrEl.Element("MinValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min))
                    da.MinValue = min;
                if (decimal.TryParse(attrEl.Element("MaxValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max))
                    da.MaxValue = max;
                if (int.TryParse(attrEl.Element("Precision")?.Value, out var prec))
                    da.Precision = prec;
                return da;
            }
            case "float" or "double":
            {
                var da = new DoubleAttributeMetadata { LogicalName = logicalName };
                if (double.TryParse(attrEl.Element("MinValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min))
                    da.MinValue = min;
                if (double.TryParse(attrEl.Element("MaxValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max))
                    da.MaxValue = max;
                if (int.TryParse(attrEl.Element("Precision")?.Value, out var prec))
                    da.Precision = prec;
                return da;
            }
            case "money":
            {
                var ma = new MoneyAttributeMetadata { LogicalName = logicalName };
                if (double.TryParse(attrEl.Element("MinValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min))
                    ma.MinValue = min;
                if (double.TryParse(attrEl.Element("MaxValue")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max))
                    ma.MaxValue = max;
                if (int.TryParse(attrEl.Element("Precision")?.Value, out var prec))
                    ma.Precision = prec;
                return ma;
            }
            case "memo":
            {
                var ma = new MemoAttributeMetadata { LogicalName = logicalName };
                if (int.TryParse(attrEl.Element("MaxLength")?.Value ?? attrEl.Element("Length")?.Value, out var maxLen))
                    ma.MaxLength = maxLen;
                return ma;
            }
            case "bit" or "boolean":
                return new BooleanAttributeMetadata { LogicalName = logicalName };

            case "picklist":
                return new PicklistAttributeMetadata { LogicalName = logicalName };

            case "state":
                return new StateAttributeMetadata { LogicalName = logicalName };

            case "status":
                return new StatusAttributeMetadata { LogicalName = logicalName };

            case "multiselectpicklist":
                return new MultiSelectPicklistAttributeMetadata { LogicalName = logicalName };

            case "bigint":
                return new BigIntAttributeMetadata { LogicalName = logicalName };

            case "image":
                return new ImageAttributeMetadata { LogicalName = logicalName };

            case "file":
                return new FileAttributeMetadata { LogicalName = logicalName };

            case "uniqueidentifier":
                return new UniqueIdentifierAttributeMetadata { LogicalName = logicalName };

            case "entityname" or "virtual":
                // EntityName and virtual types — use String as a reasonable fallback
                return new StringAttributeMetadata { LogicalName = logicalName };

            default:
                // Unknown type — use String as fallback to avoid data loss
                return new StringAttributeMetadata { LogicalName = logicalName };
        }
    }

    private static void LoadGlobalOptionSets(Workspace workspace, string rootPath)
    {
        var optionSetsDir = Path.Combine(rootPath, "OptionSets");
        if (!Directory.Exists(optionSetsDir)) return;

        foreach (var file in Directory.GetFiles(optionSetsDir, "*.xml"))
        {
            var (optionSet, doc) = ParseOptionSetFile(file);
            if (optionSet != null && doc != null)
            {
                workspace.AddGlobalOptionSet(optionSet);
                workspace.OriginalDocuments[$"OptionSet:{optionSet.Name}"] = doc;
            }
        }
    }

    private static (OptionSetMetadata? optionSet, XDocument? doc) ParseOptionSetFile(string filePath)
    {
        var doc = LoadDocument(filePath);
        var root = doc.Root; // <optionset>
        if (root == null) return (null, null);

        var name = root.Attribute("Name")?.Value;
        if (string.IsNullOrEmpty(name)) return (null, null);

        var optionSet = new OptionSetMetadata
        {
            Name = name,
            OptionSetId = root.Attribute("OptionSetId")?.Value,
            IsGlobal = root.Element("IsGlobal")?.Value == "1",
            Source = CreateSourceLocation(filePath, root)
        };

        // Display name
        var osDisplayLabel = ReadLabel(root.Element("displaynames"), "displayname");
        if (osDisplayLabel != null) optionSet.DisplayName = osDisplayLabel;

        // Description
        var osDescLabel = ReadLabel(root.Element("Descriptions"), "Description");
        if (osDescLabel != null) optionSet.Description = osDescLabel;

        // Options
        var options = root.Element("options");
        if (options != null)
        {
            foreach (var optEl in options.Elements("option"))
            {
                var valStr = optEl.Attribute("value")?.Value;
                if (!int.TryParse(valStr, out var value)) continue;

                var option = new OptionMetadata { Value = value };

                var optionLabel = ReadLabel(optEl.Element("labels"), "label");
                if (optionLabel != null) option.Label = optionLabel;

                var optionDescLabel = ReadLabel(optEl.Element("Descriptions"), "Description");
                if (optionDescLabel != null) option.Description = optionDescLabel;

                optionSet.AddOption(option);
            }
        }

        return (optionSet, doc);
    }

    private static void LoadRelationships(Workspace workspace, string rootPath)
    {
        var relationshipsByName = new Dictionary<string, RelationshipMetadata>(StringComparer.OrdinalIgnoreCase);
        var relationshipsFile = Path.Combine(rootPath, "Other", "Relationships.xml");
        if (File.Exists(relationshipsFile))
        {
            var doc = LoadDocument(relationshipsFile);
            workspace.OriginalDocuments["Relationships.xml"] = doc;
            LoadRelationshipElements(relationshipsByName, doc.Root, relationshipsFile, replaceExisting: false);
        }

        var relationshipsDir = Path.Combine(rootPath, "Other", "Relationships");
        if (Directory.Exists(relationshipsDir))
        {
            foreach (var file in Directory.GetFiles(relationshipsDir, "*.xml", SearchOption.AllDirectories))
            {
                var doc = LoadDocument(file);
                var relativePath = GetRelativePath(rootPath, file);
                workspace.OriginalDocuments[$"Relationships:{relativePath}"] = doc;
                LoadRelationshipElements(relationshipsByName, doc.Root, file, replaceExisting: true);
            }
        }

        foreach (var relationship in relationshipsByName.Values)
        {
            workspace.AddRelationship(relationship);
            AttachRelationshipToEntities(workspace, relationship);
        }
    }

    private static void LoadRelationshipElements(
        Dictionary<string, RelationshipMetadata> relationshipsByName,
        XElement? root,
        string sourceFile,
        bool replaceExisting)
    {
        if (root == null) return;

        foreach (var relEl in root.Elements("EntityRelationship"))
        {
            var relationship = ParseRelationship(relEl, sourceFile);
            if (relationship == null) continue;

            if (replaceExisting || !relationshipsByName.ContainsKey(relationship.SchemaName))
            {
                relationshipsByName[relationship.SchemaName] = relationship;
            }
        }
    }

    private static RelationshipMetadata? ParseRelationship(XElement relEl, string sourceFile)
    {
        var name = relEl.Attribute("Name")?.Value;
        if (string.IsNullOrEmpty(name)) return null;

        var entity1 = relEl.Element("Entity1LogicalName")?.Value;
        var entity2 = relEl.Element("Entity2LogicalName")?.Value;
        var intersect = relEl.Element("IntersectEntityName")?.Value;

        RelationshipMetadata relationship;
        if (entity1 != null && entity2 != null && intersect != null)
        {
            relationship = new ManyToManyRelationshipMetadata
            {
                SchemaName = name,
                Entity1LogicalName = entity1,
                Entity2LogicalName = entity2,
                IntersectEntityName = intersect
            };
        }
        else
        {
            relationship = new OneToManyRelationshipMetadata
            {
                SchemaName = name,
                ReferencedEntity = relEl.Element("ReferencedEntityName")?.Value ?? "",
                ReferencedAttribute = relEl.Element("ReferencedAttributeName")?.Value ?? "",
                ReferencingEntity = relEl.Element("ReferencingEntityName")?.Value ?? "",
                ReferencingAttribute = relEl.Element("ReferencingAttributeName")?.Value ?? "",
                CascadeAssign = ParseCascade(relEl.Element("CascadeAssign")?.Value, CascadeType.NoCascade),
                CascadeDelete = ParseCascade(relEl.Element("CascadeDelete")?.Value, CascadeType.RemoveLink),
                CascadeReparent = ParseCascade(relEl.Element("CascadeReparent")?.Value, CascadeType.NoCascade),
                CascadeShare = ParseCascade(relEl.Element("CascadeShare")?.Value, CascadeType.NoCascade),
                CascadeUnshare = ParseCascade(relEl.Element("CascadeUnshare")?.Value, CascadeType.NoCascade),
                CascadeArchive = ParseCascade(relEl.Element("CascadeArchive")?.Value, CascadeType.RemoveLink),
                CascadeRollupView = ParseCascade(relEl.Element("CascadeRollupView")?.Value, CascadeType.NoCascade),
                IsHierarchical = relEl.Element("IsHierarchical")?.Value == "1",
                IsValidForAdvancedFind = relEl.Element("IsValidForAdvancedFind")?.Value == "1"
            };
        }

        relationship.IsCustomizable = relEl.Element("IsCustomizable")?.Value == "1";
        relationship.IntroducedVersion = relEl.Element("IntroducedVersion")?.Value;
        relationship.Source = CreateSourceLocation(sourceFile, relEl);

        var description = ReadLabel(relEl.Element("RelationshipDescription")?.Element("Descriptions"), "Description");
        if (description != null) relationship.Description = description;

        foreach (var roleEl in relEl.Element("EntityRelationshipRoles")?.Elements("EntityRelationshipRole") ?? Enumerable.Empty<XElement>())
        {
            relationship.AddRole(new RelationshipRoleMetadata
            {
                NavPaneDisplayOption = roleEl.Element("NavPaneDisplayOption")?.Value,
                NavPaneArea = roleEl.Element("NavPaneArea")?.Value,
                NavPaneOrder = ParseInt(roleEl.Element("NavPaneOrder")?.Value),
                NavigationPropertyName = roleEl.Element("NavigationPropertyName")?.Value,
                RelationshipRoleType = ParseInt(roleEl.Element("RelationshipRoleType")?.Value)
            });
        }

        return relationship;
    }

    private static void AttachRelationshipToEntities(Workspace workspace, RelationshipMetadata relationship)
    {
        foreach (var entity in workspace.Entities)
        {
            if (IsRelationshipParticipant(relationship, entity.LogicalName))
            {
                entity.AddRelationship(relationship);
            }
        }
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

    private static CascadeType ParseCascade(string? value, CascadeType defaultValue)
    {
        return Enum.TryParse<CascadeType>(value, ignoreCase: true, out var cascade)
            ? cascade
            : defaultValue;
    }

    private static void LoadForms(Workspace workspace, string rootPath)
    {
        var entitiesDir = Path.Combine(rootPath, "Entities");
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityDir in Directory.GetDirectories(entitiesDir))
        {
            var entityLogicalName = Path.GetFileName(entityDir);
            var formXmlDir = Path.Combine(entityDir, "FormXml");
            if (!Directory.Exists(formXmlDir)) continue;

            foreach (var formTypeDir in Directory.GetDirectories(formXmlDir))
            {
                var formType = Path.GetFileName(formTypeDir);
                var loadedFormIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var formFile in OrderManagedVariantFiles(Directory.GetFiles(formTypeDir, "*.xml")))
                {
                    var doc = LoadDocument(formFile);
                    var systemForm = doc.Root?.Element("systemform");
                    if (systemForm == null) continue;

                    var formId = systemForm.Element("formid")?.Value ?? "";
                    if (string.IsNullOrEmpty(formId)) continue;
                    if (!loadedFormIds.Add(formId)) continue;

                    var form = new FormMetadata
                    {
                        FormId = formId,
                        FormType = formType,
                        EntityLogicalName = entityLogicalName,
                        IntroducedVersion = systemForm.Element("IntroducedVersion")?.Value,
                        FormPresentation = ParseInt(systemForm.Element("FormPresentation")?.Value, 0),
                        FormActivationState = ParseInt(systemForm.Element("FormActivationState")?.Value, 0),
                        Source = CreateSourceLocation(formFile, systemForm)
                    };

                    var displayLabel = ReadLabel(systemForm.Element("LocalizedNames"), "LocalizedName");
                    if (displayLabel != null) form.DisplayName = displayLabel;

                    var descLabel = ReadLabel(systemForm.Element("Descriptions"), "Description");
                    if (descLabel != null) form.Description = descLabel;

                    var formBody = systemForm.Element("form");
                    if (formBody != null)
                        form.Body = MergeableNodeXmlConverter.FromXElement(formBody);

                    workspace.AddForm(form);
                    workspace.OriginalDocuments[$"Form:{entityLogicalName}:{formId}"] = doc;
                }
            }
        }
    }

    private static void LoadViews(Workspace workspace, string rootPath)
    {
        var entitiesDir = Path.Combine(rootPath, "Entities");
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityDir in Directory.GetDirectories(entitiesDir))
        {
            var entityLogicalName = Path.GetFileName(entityDir);
            var savedQueriesDir = Path.Combine(entityDir, "SavedQueries");
            if (!Directory.Exists(savedQueriesDir)) continue;

            foreach (var viewFile in Directory.GetFiles(savedQueriesDir, "*.xml"))
            {
                var doc = LoadDocument(viewFile);
                var savedQuery = doc.Root?.Element("savedquery");
                if (savedQuery == null) continue;

                var savedQueryId = savedQuery.Element("savedqueryid")?.Value ?? "";
                if (string.IsNullOrEmpty(savedQueryId)) continue;

                var view = new SavedQueryMetadata
                {
                    SavedQueryId = savedQueryId,
                    EntityLogicalName = entityLogicalName,
                    QueryType = ParseInt(savedQuery.Element("querytype")?.Value, 0),
                    IsDefault = savedQuery.Element("isdefault")?.Value == "1",
                    FetchXml = savedQuery.Element("fetchxml")?.Value,
                    LayoutXml = savedQuery.Element("layoutxml")?.Value,
                    IntroducedVersion = savedQuery.Element("IntroducedVersion")?.Value,
                    Source = CreateSourceLocation(viewFile, savedQuery)
                };

                var displayLabel = ReadLabel(savedQuery.Element("LocalizedNames"), "LocalizedName");
                if (displayLabel != null) view.DisplayName = displayLabel;

                var descLabel = ReadLabel(savedQuery.Element("Descriptions"), "Description");
                if (descLabel != null) view.Description = descLabel;

                workspace.AddView(view);
                workspace.OriginalDocuments[$"View:{entityLogicalName}:{savedQueryId}"] = doc;
            }
        }
    }

    private static void LoadWebResources(Workspace workspace, string rootPath)
    {
        foreach (var (file, doc, root) in LoadXmlFiles(workspace, Path.Combine(rootPath, "WebResources"), "*.data.xml", SearchOption.AllDirectories))
        {
            var webResourceId = root.Element("WebResourceId")?.Value ?? "";
            var name = root.Element("Name")?.Value ?? "";
            if (string.IsNullOrEmpty(webResourceId) || string.IsNullOrEmpty(name)) continue;

            var webResource = new WebResourceMetadata
            {
                WebResourceId = webResourceId,
                Name = name,
                DisplayName = root.Element("DisplayName")?.Value is { Length: > 0 } displayName
                    ? new Label(displayName)
                    : new Label(),
                WebResourceType = ParseInt(root.Element("WebResourceType")?.Value, 0),
                FileName = root.Element("FileName")?.Value,
                IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                IsCustomizable = root.Element("IsCustomizable")?.Value == "1",
                CanBeDeleted = root.Element("CanBeDeleted")?.Value == "1",
                IsHidden = root.Element("IsHidden")?.Value == "1",
                IsEnabledForMobileClient = root.Element("IsEnabledForMobileClient")?.Value == "1",
                IsAvailableForMobileOffline = root.Element("IsAvailableForMobileOffline")?.Value == "1",
                Source = CreateSourceLocation(file, root)
            };

            workspace.AddWebResource(webResource);
            workspace.OriginalDocuments[$"WebResource:{name}"] = doc;
        }
    }

    private static void LoadWorkflows(Workspace workspace, string rootPath)
    {
        foreach (var (file, doc, root) in LoadXmlFiles(workspace, Path.Combine(rootPath, "Workflows"), "*.data.xml"))
        {
            var workflowId = root.Attribute("WorkflowId")?.Value ?? "";
            if (string.IsNullOrEmpty(workflowId)) continue;

            var workflow = new WorkflowMetadata
            {
                WorkflowId = workflowId,
                Category = ParseInt(root.Element("Category")?.Value, 0),
                Type = ParseInt(root.Element("Type")?.Value, 0),
                PrimaryEntity = root.Element("PrimaryEntity")?.Value,
                XamlFileName = root.Element("XamlFileName")?.Value,
                UniqueName = root.Element("UniqueName")?.Value,
                Mode = ParseInt(root.Element("Mode")?.Value, 0),
                Scope = ParseInt(root.Element("Scope")?.Value, 0),
                IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                IsCustomizable = root.Element("IsCustomizable")?.Value == "1",
                TriggerOnCreate = root.Element("TriggerOnCreate")?.Value == "1",
                TriggerOnDelete = root.Element("TriggerOnDelete")?.Value == "1",
                OnDemand = root.Element("OnDemand")?.Value == "1",
                ModernFlowType = ParseInt(root.Element("ModernFlowType")?.Value),
                JsonFileName = root.Element("JsonFileName")?.Value,
                CreateStage = ParseInt(root.Element("CreateStage")?.Value),
                UpdateStage = ParseInt(root.Element("UpdateStage")?.Value),
                DeleteStage = ParseInt(root.Element("DeleteStage")?.Value),
                Rank = ParseInt(root.Element("Rank")?.Value),
                ProcessOrder = ParseInt(root.Element("ProcessOrder")?.Value),
                Source = CreateSourceLocation(file, root)
            };

            var displayLabel = ReadLabel(root.Element("LocalizedNames"), "LocalizedName");
            if (displayLabel != null) workflow.DisplayName = displayLabel;

            var descLabel = ReadLabel(root.Element("Descriptions"), "Description");
            if (descLabel != null) workflow.Description = descLabel;

            workspace.AddWorkflow(workflow);
            workspace.OriginalDocuments[$"Workflow:{workflowId}"] = doc;
        }
    }

    private static void LoadFlowDefinitions(Workspace workspace, string rootPath)
    {
        FlowDefinitionReader.Load(workspace, rootPath);
    }

    private static void LoadPluginAssemblies(Workspace workspace, string rootPath)
    {
        foreach (var (file, doc, root) in LoadXmlFiles(workspace, Path.Combine(rootPath, "PluginAssemblies"), "*.data.xml", SearchOption.AllDirectories))
        {
            var assemblyId = root.Attribute("PluginAssemblyId")?.Value ?? "";
            if (string.IsNullOrEmpty(assemblyId)) continue;

            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            var assembly = new PluginAssemblyMetadata
            {
                PluginAssemblyId = assemblyId,
                FullName = root.Attribute("FullName")?.Value,
                Name = name,
                IsolationMode = ParseInt(root.Element("IsolationMode")?.Value, 0),
                SourceType = ParseInt(root.Element("SourceType")?.Value, 0),
                FileName = root.Element("FileName")?.Value,
                IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                CustomizationLevel = ParseInt(root.Element("CustomizationLevel")?.Value, 0),
                Source = CreateSourceLocation(file, root)
            };

            var pluginTypes = root.Element("PluginTypes");
            if (pluginTypes != null)
            {
                foreach (var ptEl in pluginTypes.Elements("PluginType"))
                {
                    var pluginTypeId = ptEl.Attribute("PluginTypeId")?.Value ?? "";
                    if (string.IsNullOrEmpty(pluginTypeId)) continue;

                    var pluginType = new PluginTypeMetadata
                    {
                        PluginTypeId = pluginTypeId,
                        Name = ptEl.Attribute("Name")?.Value,
                        AssemblyQualifiedName = ptEl.Attribute("AssemblyQualifiedName")?.Value,
                        FriendlyName = ptEl.Element("FriendlyName")?.Value,
                        TypeName = ptEl.Element("TypeName")?.Value,
                        WorkflowActivityGroupName = ptEl.Element("WorkflowActivityGroupName")?.Value,
                        Source = CreateSourceLocation(file, ptEl)
                    };

                    assembly.AddPluginType(pluginType);
                }
            }

            workspace.AddPluginAssembly(assembly);
            workspace.OriginalDocuments[$"PluginAssembly:{name}"] = doc;
        }
    }

    private static void LoadSdkMessageProcessingSteps(Workspace workspace, string rootPath)
    {
        foreach (var (file, doc, root) in LoadXmlFiles(workspace, Path.Combine(rootPath, "SdkMessageProcessingSteps")))
        {
            var stepId = root.Attribute("SdkMessageProcessingStepId")?.Value ?? "";
            if (string.IsNullOrEmpty(stepId)) continue;

            var step = new SdkMessageProcessingStepMetadata
            {
                SdkMessageProcessingStepId = stepId,
                Name = root.Attribute("Name")?.Value,
                SdkMessageId = root.Element("SdkMessageId")?.Value,
                PluginTypeName = root.Element("PluginTypeName")?.Value,
                PluginTypeId = root.Element("PluginTypeId")?.Value,
                Stage = ParseInt(root.Element("Stage")?.Value, 0),
                Mode = ParseInt(root.Element("Mode")?.Value, 0),
                Rank = ParseInt(root.Element("Rank")?.Value, 1),
                FilteringAttributes = root.Element("FilteringAttributes")?.Value,
                AsyncAutoDelete = root.Element("AsyncAutoDelete")?.Value == "1",
                Description = root.Element("Description")?.Value,
                SupportedDeployment = ParseInt(root.Element("SupportedDeployment")?.Value, 0),
                InvocationSource = ParseInt(root.Element("InvocationSource")?.Value, 0),
                IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                IsCustomizable = root.Element("IsCustomizable")?.Value == "1",
                IsHidden = root.Element("IsHidden")?.Value == "1",
                Source = CreateSourceLocation(file, root)
            };

            var imagesEl = root.Element("SdkMessageProcessingStepImages");
            if (imagesEl != null)
            {
                foreach (var imgEl in imagesEl.Elements("SdkMessageProcessingStepImage"))
                {
                    var imageId = imgEl.Attribute("SdkMessageProcessingStepImageId")?.Value ?? "";
                    if (string.IsNullOrEmpty(imageId)) continue;

                    var image = new SdkMessageProcessingStepImageMetadata
                    {
                        SdkMessageProcessingStepImageId = imageId,
                        ImageType = ParseInt(imgEl.Element("ImageType")?.Value, 0),
                        MessagePropertyName = imgEl.Element("MessagePropertyName")?.Value,
                        EntityAlias = imgEl.Element("EntityAlias")?.Value,
                        Attributes = imgEl.Element("Attributes")?.Value,
                        Source = CreateSourceLocation(file, imgEl)
                    };

                    step.AddImage(image);
                }
            }

            workspace.AddSdkMessageProcessingStep(step);
            workspace.OriginalDocuments[$"Step:{stepId}"] = doc;
        }
    }

    private static void LoadSecurityRoles(Workspace workspace, string rootPath)
    {
        foreach (var (file, doc, root) in LoadXmlFiles(workspace, Path.Combine(rootPath, "Roles")))
        {
            var roleId = root.Attribute("id")?.Value ?? "";
            var roleName = root.Attribute("name")?.Value ?? "";
            if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(roleName)) continue;

            var role = new SecurityRoleMetadata
            {
                RoleId = roleId,
                Name = roleName,
                IsInherited = root.Attribute("isinherited")?.Value == "1",
                IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                Source = CreateSourceLocation(file, root)
            };

            var privilegesEl = root.Element("RolePrivileges");
            if (privilegesEl != null)
            {
                foreach (var privEl in privilegesEl.Elements("RolePrivilege"))
                {
                    var privName = privEl.Attribute("name")?.Value;
                    var privLevel = privEl.Attribute("level")?.Value;
                    if (string.IsNullOrEmpty(privName) || string.IsNullOrEmpty(privLevel)) continue;

                    role.AddPrivilege(new RolePrivilegeMetadata
                    {
                        Name = privName!,
                        Level = privLevel!
                    });
                }
            }

            workspace.AddSecurityRole(role);
            workspace.OriginalDocuments[$"Role:{roleId}"] = doc;
        }
    }

    private static void LoadAppModules(Workspace workspace, string rootPath)
    {
        var appModulesDir = Path.Combine(rootPath, "AppModules");
        if (!Directory.Exists(appModulesDir)) return;

        foreach (var appModuleDir in Directory.GetDirectories(appModulesDir))
        {
            var loadedUniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var appModuleFile in OrderManagedVariantFiles(Directory.GetFiles(appModuleDir, "AppModule*.xml", SearchOption.TopDirectoryOnly)))
            {
                var doc = LoadDocument(appModuleFile);
                var root = doc.Root; // <AppModule>
                if (root == null) continue;

                var uniqueName = root.Element("UniqueName")?.Value ?? "";
                if (string.IsNullOrEmpty(uniqueName)) continue;
                if (!loadedUniqueNames.Add(uniqueName)) continue;

                var appModule = new AppModuleMetadata
                {
                    UniqueName = uniqueName,
                    IntroducedVersion = root.Element("IntroducedVersion")?.Value,
                    WebResourceId = root.Element("WebResourceId")?.Value,
                    FormFactor = ParseInt(root.Element("FormFactor")?.Value, 0),
                    ClientType = ParseInt(root.Element("ClientType")?.Value, 0),
                    NavigationType = ParseInt(root.Element("NavigationType")?.Value, 0),
                    Source = CreateSourceLocation(appModuleFile, root)
                };

                var displayLabel = ReadLabel(root.Element("LocalizedNames"), "LocalizedName");
                if (displayLabel != null) appModule.DisplayName = displayLabel;
                appModule.Body = MergeableNodeXmlConverter.FromXElement(root);

                var componentsEl = root.Element("AppModuleComponents");
                if (componentsEl != null)
                {
                    foreach (var compEl in componentsEl.Elements("AppModuleComponent"))
                    {
                        var typeStr = compEl.Attribute("type")?.Value;
                        if (!int.TryParse(typeStr, out var typeCode)) continue;

                        var component = new AppModuleComponent
                        {
                            Type = typeCode,
                            SchemaName = compEl.Attribute("schemaName")?.Value,
                            Id = compEl.Attribute("id")?.Value
                        };

                        appModule.AddComponent(component);
                    }
                }

                var roleMapsEl = root.Element("AppModuleRoleMaps");
                if (roleMapsEl != null)
                {
                    foreach (var roleEl in roleMapsEl.Elements("Role"))
                    {
                        var roleId = roleEl.Attribute("id")?.Value;
                        if (!string.IsNullOrEmpty(roleId))
                            appModule.AddRoleId(roleId!);
                    }
                }

                workspace.AddAppModule(appModule);
                workspace.OriginalDocuments[$"AppModule:{uniqueName}"] = doc;
            }
        }
    }

    private static void LoadSiteMaps(Workspace workspace, string rootPath)
    {
        var loadedUniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var siteMapDir = Path.Combine(rootPath, "AppModuleSiteMaps");
        if (Directory.Exists(siteMapDir))
        {
            foreach (var subDir in Directory.GetDirectories(siteMapDir))
            {
                foreach (var file in OrderManagedVariantFiles(Directory.GetFiles(subDir, "*.xml")))
                {
                    LoadSiteMapFile(workspace, file, loadedUniqueNames);
                }
            }
        }

        var legacySiteMaps = new[]
        {
            Path.Combine(rootPath, "Other", "SiteMap.xml"),
            Path.Combine(rootPath, "Other", "SiteMap_managed.xml")
        }.Where(File.Exists);
        foreach (var file in OrderManagedVariantFiles(legacySiteMaps))
        {
            LoadSiteMapFile(workspace, file, loadedUniqueNames);
        }
    }

    private static void LoadSiteMapFile(Workspace workspace, string file, ISet<string> loadedUniqueNames)
    {
        var doc = LoadDocument(file);
        var root = doc.Root; // <AppModuleSiteMap>
        if (root == null) return;

        var uniqueName = root.Element("SiteMapUniqueName")?.Value ?? Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrEmpty(uniqueName)) return;
        if (!loadedUniqueNames.Add(uniqueName)) return;

        var siteMap = new SiteMapMetadata
        {
            UniqueName = uniqueName,
            IntroducedVersion = root.Element("SiteMap")?.Attribute("IntroducedVersion")?.Value,
            EnableCollapsibleGroups = root.Element("EnableCollapsibleGroups")?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true,
            ShowHome = root.Element("ShowHome")?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true,
            ShowPinned = root.Element("ShowPinned")?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true,
            ShowRecents = root.Element("ShowRecents")?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true,
            Source = CreateSourceLocation(file, root)
        };

        var displayLabel = ReadLabel(root.Element("LocalizedNames"), "LocalizedName");
        if (displayLabel != null) siteMap.DisplayName = displayLabel;
        siteMap.Body = MergeableNodeXmlConverter.FromXElement(root);

        workspace.AddSiteMap(siteMap);
        workspace.OriginalDocuments[$"SiteMap:{uniqueName}"] = doc;
    }

    private static void LoadRoundtripPassthroughFiles(Workspace workspace, string rootPath)
    {
        var customizationsFile = Path.Combine(rootPath, "Other", "Customizations.xml");
        if (File.Exists(customizationsFile))
        {
            LoadGenericComponentFile(workspace, customizationsFile, GetRelativePath(rootPath, customizationsFile));
        }

    }

    private static void LoadRibbons(Workspace workspace, string rootPath)
    {
        var entitiesDir = Path.Combine(rootPath, "Entities");
        if (!Directory.Exists(entitiesDir)) return;

        foreach (var entityDir in Directory.GetDirectories(entitiesDir))
        {
            var entityLogicalName = Path.GetFileName(entityDir);
            foreach (var ribbonDiffFile in Directory.GetFiles(entityDir, "RibbonDiff.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try
                {
                    doc = LoadDocument(ribbonDiffFile);
                }
                catch (System.Xml.XmlException ex)
                {
                    workspace.AddLoadError(ribbonDiffFile, $"Malformed XML: {ex.Message}");
                    continue;
                }

                var root = doc.Root;
                if (root == null) continue;

                var ribbon = new RibbonMetadata
                {
                    EntityLogicalName = entityLogicalName,
                    Body = MergeableNodeXmlConverter.FromXElement(root),
                    Source = CreateSourceLocation(ribbonDiffFile, root)
                };
                workspace.AddRibbon(ribbon);
                workspace.OriginalDocuments[$"Ribbon:{entityLogicalName}"] = doc;
            }
        }
    }

    /// <summary>
    /// Reads all localized label entries from a container element.
    /// Handles all Dataverse XML patterns: LocalizedNames/LocalizedName,
    /// displaynames/displayname, labels/label, Descriptions/Description, etc.
    /// </summary>
    private static Label? ReadLabel(XElement? container, string childName,
        string textAttribute = "description", string lcidAttribute = "languagecode")
    {
        if (container == null) return null;
        var children = container.Elements(childName).ToList();
        if (children.Count == 0) return null;

        var label = new Label();
        foreach (var child in children)
        {
            var text = child.Attribute(textAttribute)?.Value;
            var lcid = ParseInt(child.Attribute(lcidAttribute)?.Value, 0);
            if (text != null && lcid > 0)
                label[lcid] = text;
        }
        return label.LocalizedLabels.Count > 0 ? label : null;
    }

    /// <summary>
    /// Directories already handled by dedicated loaders.
    /// </summary>
    private static readonly HashSet<string> DedicatedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Entities", "OptionSets", "WebResources", "Workflows",
        "PluginAssemblies", "SdkMessageProcessingSteps", "Roles",
        "AppModules", "AppModuleSiteMaps", "Other"
    };

    /// <summary>
    /// Directories that should never be scanned by the generic component loader
    /// (build artifacts, IDE folders, package caches, etc.).
    /// </summary>
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".github", "node_modules", "packages", "TestResults"
    };

    /// <summary>
    /// Specific files in Other/ that are handled by dedicated loaders.
    /// </summary>
    private static readonly HashSet<string> DedicatedOtherFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Solution.xml", "Relationships.xml", "Customizations.xml", "SiteMap.xml"
    };

    /// <summary>
    /// Scans the workspace for XML files not already covered by dedicated loaders
    /// and loads them as generic components to preserve through roundtrip.
    /// </summary>
    private static void LoadGenericComponents(Workspace workspace, string rootPath)
    {
        // Scan Other/ directory for files not handled by dedicated loaders
        var otherDir = Path.Combine(rootPath, "Other");
        if (Directory.Exists(otherDir))
        {
            foreach (var file in Directory.GetFiles(otherDir, "*.xml"))
            {
                var fileName = Path.GetFileName(file);
                if (DedicatedOtherFiles.Contains(fileName)) continue;

                var relativePath = Path.Combine("Other", fileName);
                LoadGenericComponentFile(workspace, file, relativePath);
            }
        }

        // Scan top-level directories that aren't covered by dedicated loaders
        foreach (var dir in Directory.GetDirectories(rootPath))
        {
            var dirName = Path.GetFileName(dir);
            if (DedicatedDirectories.Contains(dirName)) continue;
            if (IgnoredDirectories.Contains(dirName)) continue;
            if (dirName.StartsWith(".", StringComparison.Ordinal)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
                LoadGenericComponentFile(workspace, file, relativePath);
            }
        }
    }

    private static void LoadGenericComponentFile(Workspace workspace, string file, string relativePath)
    {
        var key = $"Generic:{relativePath}";
        if (workspace.OriginalDocuments.ContainsKey(key)) return;

        XDocument doc;
        try
        {
            doc = LoadDocument(file);
        }
        catch (System.Xml.XmlException ex)
        {
            workspace.AddLoadError(file, $"Malformed XML: {ex.Message}");
            return;
        }

        var root = doc.Root;
        if (root == null) return;

        var componentTypeName = root.Name.LocalName;

        // Try to extract an ID from common patterns
        string? id = root.Attribute("id")?.Value
            ?? root.Attribute("Id")?.Value
            ?? root.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Id"))?.Value;

        // Try to extract a name
        string? name = root.Attribute("name")?.Value
            ?? root.Attribute("Name")?.Value
            ?? root.Element("Name")?.Value
            ?? root.Element("SchemaName")?.Value
            ?? root.Element("UniqueName")?.Value;

        var component = new GenericComponentMetadata
        {
            ComponentTypeName = componentTypeName,
            Id = id,
            Name = name,
            FilePath = relativePath,
            Source = CreateSourceLocation(file, root),
            SerializedContent = doc.ToString()
        };

        workspace.AddGenericComponent(component);
        workspace.OriginalDocuments[key] = doc;
    }

    private static bool IsManagedVariantFile(string filePath)
    {
        return Path.GetFileName(filePath).EndsWith("_managed.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> OrderManagedVariantFiles(IEnumerable<string> filePaths)
    {
        return filePaths
            .OrderByDescending(IsManagedVariantFile)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Enumerates XML files in a directory, loading each as an XDocument with PreserveWhitespace and SetLineInfo.
    /// Skips files that are malformed XML or have no root element.
    /// </summary>
    private static IEnumerable<(string filePath, XDocument doc, XElement root)> LoadXmlFiles(
        Workspace workspace, string directory, string pattern = "*.xml",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(directory)) yield break;
        foreach (var file in Directory.EnumerateFiles(directory, pattern, searchOption))
        {
            XDocument doc;
            try { doc = LoadDocument(file); }
            catch (System.Xml.XmlException ex)
            {
                workspace.AddLoadError(file, $"Malformed XML: {ex.Message}");
                continue;
            }
            var root = doc.Root;
            if (root == null) continue;
            yield return (file, doc, root);
        }
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static XDocument LoadDocument(string filePath) => XDocument.Load(filePath, XmlLoadOptions);

    private static SourceLocation CreateSourceLocation(string filePath, XObject? source)
    {
        if (source is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
            return new SourceLocation(filePath, lineInfo.LineNumber, lineInfo.LinePosition);

        return new SourceLocation(filePath, 1, 1);
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        return filePath.Substring(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
    }
}
