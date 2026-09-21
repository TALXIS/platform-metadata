using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Emits the &lt;noPrefixEntitySetName&gt;.Schema.json Dataverse schema from
/// the model solution's Entity.xml using System.Text.Json.
/// </summary>
internal static class CodeAppSchemaGenerator
{
    public static void Generate(string modelSolutionRootPath, string entityLogicalName, string outputPath)
    {
        var entityXmlPath = Path.Combine(modelSolutionRootPath, "Entities", entityLogicalName, "Entity.xml");
        if (!File.Exists(entityXmlPath))
            throw new FileNotFoundException($"Entity.xml not found: {entityXmlPath}");

        var doc = XDocument.Load(entityXmlPath);
        var entityNode = doc.Root!.Element("EntityInfo")!.Element("entity")!;
        var entitySetName = entityNode.Element("EntitySetName")!.Value;
        var entityName = entityNode.Attribute("Name")!.Value;

        var collectionTitle = entityNode.Element("LocalizedCollectionNames")
            ?.Elements("LocalizedCollectionName")
            .FirstOrDefault(e => e.Attribute("languagecode")?.Value == "1033")
            ?.Attribute("description")?.Value ?? entitySetName;

        string? primaryKeyName = null;
        string? primaryNameField = null;
        var attrs = new List<AttrInfo>();

        foreach (var attr in entityNode.Element("attributes")!.Elements("attribute"))
        {
            var info = new AttrInfo
            {
                PhysicalName = attr.Attribute("PhysicalName")?.Value ?? "",
                LogicalName = attr.Element("LogicalName")?.Value ?? "",
                Type = attr.Element("Type")?.Value ?? "",
                RequiredLevel = attr.Element("RequiredLevel")?.Value ?? "none",
                ValidForCreateApi = attr.Element("ValidForCreateApi")?.Value == "1",
                ValidForUpdateApi = attr.Element("ValidForUpdateApi")?.Value == "1",
                IsCustomField = attr.Element("IsCustomField")?.Value == "1",
                IsLogical = attr.Element("IsLogical")?.Value == "1",
                HasDisplayMask = !string.IsNullOrEmpty(attr.Element("DisplayMask")?.Value),
                DisplayName = attr.Element("displaynames")?.Elements("displayname")
                    .FirstOrDefault(e => e.Attribute("languagecode")?.Value == "1033")
                    ?.Attribute("description")?.Value ?? "",
            };

            var displayMask = attr.Element("DisplayMask")?.Value ?? "";
            if (displayMask.Contains("PrimaryName"))
                primaryNameField = info.LogicalName;

            var maxLenEl = attr.Element("MaxLength");
            if (maxLenEl != null && int.TryParse(maxLenEl.Value, out int ml))
                info.MaxLength = ml;

            var optEl = attr.Element("optionset");
            if (optEl != null)
            {
                info.Options = new List<(int val, string label)>();
                info.OptionSetDisplayName = optEl.Element("displaynames")?.Elements("displayname")
                    .FirstOrDefault(e => e.Attribute("languagecode")?.Value == "1033")
                    ?.Attribute("description")?.Value ?? info.DisplayName;
                info.IsGlobalOptionSet = false;

                foreach (var s in optEl.Descendants("state"))
                {
                    info.Options.Add((int.Parse(s.Attribute("value")!.Value),
                        s.Descendants("label").FirstOrDefault()?.Attribute("description")?.Value ?? ""));
                }
                foreach (var s in optEl.Descendants("status"))
                {
                    info.Options.Add((int.Parse(s.Attribute("value")!.Value),
                        s.Descendants("label").FirstOrDefault()?.Attribute("description")?.Value ?? ""));
                }
                foreach (var o in optEl.Elements("options").Elements("option"))
                {
                    info.Options.Add((int.Parse(o.Attribute("value")!.Value),
                        o.Descendants("label").FirstOrDefault()?.Attribute("description")?.Value ?? ""));
                }
            }

            var globalRef = attr.Element("OptionSetName");
            if (globalRef != null && !string.IsNullOrEmpty(globalRef.Value))
            {
                info.IsGlobalOptionSet = true;
                var globalPath = Path.Combine(modelSolutionRootPath, "OptionSets", globalRef.Value + ".xml");
                if (File.Exists(globalPath))
                {
                    var gDoc = XDocument.Load(globalPath);
                    info.Options = new List<(int val, string label)>();
                    info.OptionSetDisplayName = gDoc.Root!.Element("displaynames")?.Elements("displayname")
                        .FirstOrDefault(e => e.Attribute("languagecode")?.Value == "1033")
                        ?.Attribute("description")?.Value ?? info.DisplayName;
                    foreach (var o in gDoc.Root.Elements("options").Elements("option"))
                    {
                        info.Options.Add((int.Parse(o.Attribute("value")!.Value),
                            o.Descendants("label").FirstOrDefault()?.Attribute("description")?.Value ?? ""));
                    }
                }
            }

            if (info.Type == "primarykey")
                primaryKeyName = info.LogicalName;

            attrs.Add(info);
        }

        var properties = new SortedDictionary<string, JsonObject>();
        var requiredFields = new SortedSet<string>();
        var systemUserLookups = new HashSet<string> { "createdby", "createdonbehalfby", "modifiedby", "modifiedonbehalfby" };

        foreach (var a in attrs)
        {
            var prop = new JsonObject { ["type"] = "string", ["title"] = a.DisplayName };
            prop["x-ms-dataverse-attribute"] = a.LogicalName;
            prop["x-ms-dataverse-type"] = MapType(a.Type);
            prop["x-ms-schema-name"] = SchemaName(a);

            if (a.Type == "primarykey")
            {
                prop["x-ms-dataverse-primary-id"] = true;
                prop["required"] = true;
                prop["x-ms-keyType"] = "primary";
                requiredFields.Add(a.LogicalName);
            }
            else
            {
                if (IsReadOnly(a))
                    prop["x-ms-read-only"] = true;
                if (a.LogicalName == primaryNameField)
                    prop["x-ms-dataverse-primary-name"] = true;
                if (IsRequired(a))
                {
                    prop["required"] = true;
                    requiredFields.Add(a.LogicalName);
                }
                if (a.Type == "nvarchar" && a.MaxLength > 0)
                    prop["maxLength"] = a.MaxLength;
            }

            if ((a.Type == "state" || a.Type == "status" || a.Type == "picklist") && a.Options != null)
            {
                var enumArr = new JsonArray();
                var valArr = new JsonArray();
                var colorArr = new JsonArray();
                foreach (var (v, l) in a.Options)
                {
                    enumArr.Add(l);
                    valArr.Add(v);
                    colorArr.Add((JsonNode?)null);
                }
                prop["enum"] = enumArr;
                prop["x-ms-enum-values"] = valArr;
                prop["Color"] = colorArr;
                prop["x-ms-optionsetmetadataid"] = Guid.NewGuid().ToString();
                prop["x-ms-isGlobal"] = a.IsGlobalOptionSet;
                prop["x-ms-optionsetdisplayname"] = a.OptionSetDisplayName ?? a.DisplayName;
                prop["x-ms-optionSetType"] = OptionSetType(a.Type);
            }

            properties[a.LogicalName] = prop;

            if (a.Type == "owner")
            {
                AddStringVirtual(properties, requiredFields, a.LogicalName + "name",
                    "OwnerIdName", readOnly: true, required: true, maxLen: 100);
                var typeProp = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = a.LogicalName + "type",
                    ["x-ms-dataverse-attribute"] = a.LogicalName + "type",
                    ["x-ms-dataverse-type"] = "EntityNameType",
                    ["x-ms-schema-name"] = "OwnerIdType",
                    ["required"] = true,
                };
                properties[a.LogicalName + "type"] = typeProp;
                requiredFields.Add(a.LogicalName + "type");
                AddStringVirtual(properties, requiredFields, a.LogicalName + "yominame",
                    "OwnerIdYomiName", readOnly: true, required: true, maxLen: 100);
            }
            else if (a.Type == "lookup" && !a.IsCustomField && !a.IsLogical && a.HasDisplayMask)
            {
                AddStringVirtual(properties, requiredFields, a.LogicalName + "name",
                    a.PhysicalName + "Name", readOnly: true, required: false, maxLen: 100);
                if (systemUserLookups.Contains(a.LogicalName))
                {
                    AddStringVirtual(properties, requiredFields, a.LogicalName + "yominame",
                        a.PhysicalName + "YomiName", readOnly: true, required: true, maxLen: 100);
                }
            }
            else if (a.Type == "lookup" && a.IsCustomField)
            {
                AddStringVirtual(properties, requiredFields, a.LogicalName + "name",
                    a.LogicalName + "Name", readOnly: true, required: false, maxLen: 100);
            }

            if (a.LogicalName == "owningbusinessunit")
            {
                AddStringVirtual(properties, requiredFields, "owningbusinessunitname",
                    "OwningBusinessUnitName", readOnly: true, required: true, maxLen: 100);
            }

            if (a.Type == "state" || a.Type == "status" || a.Type == "picklist")
            {
                var virtualProp = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = a.LogicalName + "name",
                    ["x-ms-dataverse-attribute"] = a.LogicalName + "name",
                    ["x-ms-dataverse-type"] = "VirtualType",
                    ["x-ms-schema-name"] = a.LogicalName + "Name",
                    ["x-ms-read-only"] = true,
                };
                properties[a.LogicalName + "name"] = virtualProp;
            }
        }

        properties["versionnumber"] = new JsonObject
        {
            ["type"] = "string",
            ["title"] = "Version Number",
            ["x-ms-dataverse-attribute"] = "versionnumber",
            ["x-ms-dataverse-type"] = "BigIntType",
            ["x-ms-schema-name"] = "VersionNumber",
            ["x-ms-read-only"] = true,
        };

        var propsObj = new JsonObject();
        foreach (var kvp in properties)
            propsObj[kvp.Key] = kvp.Value;

        var reqArr = new JsonArray();
        foreach (var r in requiredFields)
            reqArr.Add(r);

        var itemsObj = new JsonObject
        {
            ["type"] = "object",
            ["x-ms-dataverse-entity"] = true,
            ["x-ms-dataverse-entityset"] = entitySetName,
            ["x-ms-dataverse-primary-id"] = primaryKeyName,
            ["x-ms-dataverse-primary-name"] = primaryNameField ?? "udpp_name",
            ["properties"] = propsObj,
            ["required"] = reqArr,
        };

        var root = new JsonObject
        {
            ["name"] = entityName,
            ["title"] = collectionTitle,
            ["schema"] = new JsonObject { ["type"] = "array", ["items"] = itemsObj },
        };

        Directory.CreateDirectory(outputPath);

        var noPrefixName = entitySetName;
        var underscoreIdx = entitySetName.IndexOf('_');
        if (underscoreIdx >= 0)
            noPrefixName = entitySetName.Substring(underscoreIdx + 1);

        var outputFile = Path.Combine(outputPath, noPrefixName + ".Schema.json");
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputFile, json);
    }

    private static string MapType(string t) => t switch
    {
        "lookup" => "LookupType",
        "datetime" => "DateTimeType",
        "int" => "IntegerType",
        "nvarchar" => "StringType",
        "primarykey" => "UniqueidentifierType",
        "state" => "StateType",
        "status" => "StatusType",
        "picklist" => "PicklistType",
        "owner" => "OwnerType",
        _ => "StringType",
    };

    private static string OptionSetType(string t) => t switch
    {
        "state" => "State",
        "status" => "Status",
        _ => "Picklist",
    };

    private static string SchemaName(AttrInfo a)
    {
        if (a.Type == "primarykey")
        {
            if (a.LogicalName.EndsWith("id"))
                return a.LogicalName.Substring(0, a.LogicalName.Length - 2) + "Id";
            return a.PhysicalName;
        }
        return a.IsCustomField ? a.LogicalName : a.PhysicalName;
    }

    private static bool IsReadOnly(AttrInfo a) => !a.ValidForCreateApi && !a.ValidForUpdateApi && a.Type != "primarykey";

    private static bool IsRequired(AttrInfo a) => a.RequiredLevel == "required" || a.RequiredLevel == "systemrequired";

    private static void AddStringVirtual(SortedDictionary<string, JsonObject> props, SortedSet<string> req,
        string logicalName, string schemaName, bool readOnly, bool required, int maxLen)
    {
        var p = new JsonObject
        {
            ["type"] = "string",
            ["title"] = logicalName,
            ["x-ms-dataverse-attribute"] = logicalName,
            ["x-ms-dataverse-type"] = "StringType",
            ["x-ms-schema-name"] = schemaName,
        };
        if (readOnly) p["x-ms-read-only"] = true;
        if (required) { p["required"] = true; req.Add(logicalName); }
        if (maxLen > 0) p["maxLength"] = maxLen;
        props[logicalName] = p;
    }

    private sealed class AttrInfo
    {
        public string PhysicalName = "";
        public string LogicalName = "";
        public string Type = "";
        public string RequiredLevel = "none";
        public bool ValidForCreateApi;
        public bool ValidForUpdateApi;
        public bool IsCustomField;
        public bool IsLogical;
        public bool HasDisplayMask;
        public string DisplayName = "";
        public int MaxLength;
        public List<(int val, string label)>? Options;
        public string? OptionSetDisplayName;
        public bool IsGlobalOptionSet;
    }
}
