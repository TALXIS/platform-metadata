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

        return workspace;
    }

    private static void LoadSolution(Workspace workspace, string rootPath)
    {
        var solutionFile = Path.Combine(rootPath, "Other", "Solution.xml");
        if (!File.Exists(solutionFile)) return;

        var doc = XDocument.Load(solutionFile, LoadOptions.PreserveWhitespace);
        workspace.OriginalDocuments["Solution.xml"] = doc;
        var manifest = doc.Root?.Element("SolutionManifest");
        if (manifest == null) return;

        var solution = new Solution
        {
            UniqueName = manifest.Element("UniqueName")?.Value ?? "Unknown",
            Version = manifest.Element("Version")?.Value ?? "1.0.0.0",
            IsManaged = manifest.Element("Managed")?.Value == "1",
            Source = new SourceLocation(solutionFile, 1, 1)
        };

        // Display name
        var localizedName = manifest.Element("LocalizedNames")?.Element("LocalizedName");
        if (localizedName != null)
        {
            var desc = localizedName.Attribute("description")?.Value;
            var lcid = ParseInt(localizedName.Attribute("languagecode")?.Value, 1033);
            if (desc != null) solution.DisplayName[lcid] = desc;
        }

        // Publisher
        var pubEl = manifest.Element("Publisher");
        if (pubEl != null)
        {
            var publisher = new Publisher
            {
                UniqueName = pubEl.Element("UniqueName")?.Value ?? "Unknown",
                Prefix = pubEl.Element("CustomizationPrefix")?.Value ?? "new",
                Source = new SourceLocation(solutionFile, 1, 1)
            };

            var pubName = pubEl.Element("LocalizedNames")?.Element("LocalizedName");
            if (pubName != null)
            {
                var desc = pubName.Attribute("description")?.Value;
                var lcid = ParseInt(pubName.Attribute("languagecode")?.Value, 1033);
                if (desc != null) publisher.DisplayName[lcid] = desc;
            }

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
        var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
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
            Source = new SourceLocation(filePath, 1, 1)
        };

        // Display name from <Name LocalizedName="...">
        var nameEl = root.Element("Name");
        var localizedNameAttr = nameEl?.Attribute("LocalizedName")?.Value;
        if (localizedNameAttr != null)
            entity.DisplayName = new Label(localizedNameAttr);

        // Localized names from EntityInfo
        var locName = entityInfo.Element("LocalizedNames")?.Element("LocalizedName");
        if (locName != null)
        {
            var desc = locName.Attribute("description")?.Value;
            var lcid = ParseInt(locName.Attribute("languagecode")?.Value, 1033);
            if (desc != null) entity.DisplayName[lcid] = desc;
        }

        // Plural name
        var pluralName = entityInfo.Element("LocalizedCollectionNames")?.Element("LocalizedCollectionName");
        if (pluralName != null)
        {
            var desc = pluralName.Attribute("description")?.Value;
            var lcid = ParseInt(pluralName.Attribute("languagecode")?.Value, 1033);
            if (desc != null) entity.PluralName[lcid] = desc;
        }

        // Description
        var descEl = entityInfo.Element("Descriptions")?.Element("Description");
        if (descEl != null)
        {
            var desc = descEl.Attribute("description")?.Value;
            var lcid = ParseInt(descEl.Attribute("languagecode")?.Value, 1033);
            if (!string.IsNullOrEmpty(desc)) entity.Description[lcid] = desc;
        }

        // Entity-level properties
        entity.EntitySetName = entityInfo.Element("EntitySetName")?.Value;
        entity.IsActivity = entityInfo.Element("IsActivity")?.Value == "1";
        entity.IsAuditEnabled = entityInfo.Element("IsAuditEnabled")?.Value == "1";
        entity.ChangeTrackingEnabled = entityInfo.Element("ChangeTrackingEnabled")?.Value == "1";

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
        attr.Source = new SourceLocation(filePath, 1, 1);

        // Required level
        var reqLevel = attrEl.Element("RequiredLevel")?.Value?.ToLowerInvariant();
        attr.RequiredLevel = reqLevel switch
        {
            "required" or "systemrequired" or "applicationrequired" => RequiredLevel.Required,
            "recommended" => RequiredLevel.Recommended,
            _ => RequiredLevel.None
        };

        // Display name
        var displayName = attrEl.Element("displaynames")?.Element("displayname");
        if (displayName != null)
        {
            var desc = displayName.Attribute("description")?.Value;
            var lcid = ParseInt(displayName.Attribute("languagecode")?.Value, 1033);
            if (desc != null) attr.DisplayName[lcid] = desc;
        }

        // Description
        var descEl = attrEl.Element("Descriptions")?.Element("Description");
        if (descEl != null)
        {
            var desc = descEl.Attribute("description")?.Value;
            var lcid = ParseInt(descEl.Attribute("languagecode")?.Value, 1033);
            if (!string.IsNullOrEmpty(desc)) attr.Description[lcid] = desc;
        }

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
        var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        var root = doc.Root; // <optionset>
        if (root == null) return (null, null);

        var name = root.Attribute("Name")?.Value;
        if (string.IsNullOrEmpty(name)) return (null, null);

        var optionSet = new OptionSetMetadata
        {
            Name = name,
            IsGlobal = root.Element("IsGlobal")?.Value == "1",
            Source = new SourceLocation(filePath, 1, 1)
        };

        // Display name
        var displayName = root.Element("displaynames")?.Element("displayname");
        if (displayName != null)
        {
            var desc = displayName.Attribute("description")?.Value;
            var lcid = ParseInt(displayName.Attribute("languagecode")?.Value, 1033);
            if (desc != null) optionSet.DisplayName[lcid] = desc;
        }

        // Description
        var descEl = root.Element("Descriptions")?.Element("Description");
        if (descEl != null)
        {
            var desc = descEl.Attribute("description")?.Value;
            var lcid = ParseInt(descEl.Attribute("languagecode")?.Value, 1033);
            if (!string.IsNullOrEmpty(desc)) optionSet.Description[lcid] = desc;
        }

        // Options
        var options = root.Element("options");
        if (options != null)
        {
            foreach (var optEl in options.Elements("option"))
            {
                var valStr = optEl.Attribute("value")?.Value;
                if (!int.TryParse(valStr, out var value)) continue;

                var option = new OptionMetadata { Value = value };

                var label = optEl.Element("labels")?.Element("label");
                if (label != null)
                {
                    var desc = label.Attribute("description")?.Value;
                    var lcid = ParseInt(label.Attribute("languagecode")?.Value, 1033);
                    if (desc != null) option.Label[lcid] = desc;
                }

                var optDesc = optEl.Element("Descriptions")?.Element("Description");
                if (optDesc != null)
                {
                    var desc = optDesc.Attribute("description")?.Value;
                    var lcid = ParseInt(optDesc.Attribute("languagecode")?.Value, 1033);
                    if (!string.IsNullOrEmpty(desc)) option.Description[lcid] = desc;
                }

                optionSet.AddOption(option);
            }
        }

        return (optionSet, doc);
    }

    private static void LoadRelationships(Workspace workspace, string rootPath)
    {
        var relationshipsFile = Path.Combine(rootPath, "Other", "Relationships.xml");
        if (!File.Exists(relationshipsFile)) return;

        var doc = XDocument.Load(relationshipsFile, LoadOptions.PreserveWhitespace);
        workspace.OriginalDocuments["Relationships.xml"] = doc;
        var root = doc.Root; // <EntityRelationships>
        if (root == null) return;

        foreach (var relEl in root.Elements("EntityRelationship"))
        {
            var name = relEl.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            // The minimal format only has a Name attribute.
            // Full format may include child elements with details.
            var referencedEntity = relEl.Element("ReferencedEntityName")?.Value;
            var referencedAttr = relEl.Element("ReferencedAttributeName")?.Value;
            var referencingEntity = relEl.Element("ReferencingEntityName")?.Value;
            var referencingAttr = relEl.Element("ReferencingAttributeName")?.Value;
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
                    IntersectEntityName = intersect,
                    Source = new SourceLocation(relationshipsFile, 1, 1)
                };
            }
            else
            {
                relationship = new OneToManyRelationshipMetadata
                {
                    SchemaName = name,
                    ReferencedEntity = referencedEntity ?? "",
                    ReferencedAttribute = referencedAttr ?? "",
                    ReferencingEntity = referencingEntity ?? "",
                    ReferencingAttribute = referencingAttr ?? "",
                    Source = new SourceLocation(relationshipsFile, 1, 1)
                };
            }

            workspace.AddRelationship(relationship);
        }
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}
