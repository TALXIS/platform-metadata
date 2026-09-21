using System.Text;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class AppCodeDataScaffoldTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("metadata-appcodedata").FullName;
    private readonly string _app;
    private readonly string _model;

    public AppCodeDataScaffoldTests()
    {
        _app = Path.Combine(_root, "app");
        _model = Path.Combine(_root, "model");
        Directory.CreateDirectory(Path.Combine(_app, "src", "generated", "services"));
        Directory.CreateDirectory(Path.Combine(_app, "payload", "models"));
        Directory.CreateDirectory(Path.Combine(_model, "Entities", "udpp_warehouseitem"));
        Directory.CreateDirectory(Path.Combine(_model, "OptionSets"));

        File.WriteAllText(Path.Combine(_app, "power.config.json"), """
            {
              "version": "1.0",
              "appId": "",
              "connectionReferences": {},
              "databaseReferences": {}
            }
            """);

        File.WriteAllText(Path.Combine(_app, "payload", "index.ts"),
            "/*!\r\n * Header\r\n */\r\n\r\n// Models\r\nexport * as CommonModels from './models/CommonModels';\r\n\r\n\r\n// Services\r\n");
        File.WriteAllText(Path.Combine(_app, "payload", "models", "CommonModels.ts"), "export interface IGetOptions {}\n");

        File.WriteAllText(ServiceTemplatePath(),
            "import { capitalizedentitylogicalnameexamplesBase } from '../models/capitalizedentitylogicalnameexamplesModel';\n" +
            "export class capitalizedentitylogicalnameexamplesService {\n" +
            "  private static readonly dataSourceName = 'lowercaseentitylogicalnameexamples';\n" +
            "}\n");

        File.WriteAllText(Path.Combine(_model, "Entities", "udpp_warehouseitem", "Entity.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity>
              <Name LocalizedName="Warehouse Item" OriginalName="Warehouse Item">udpp_warehouseitem</Name>
              <EntityInfo>
                <entity Name="udpp_warehouseitem">
                  <EntitySetName>udpp_warehouseitems</EntitySetName>
                  <LocalizedCollectionNames>
                    <LocalizedCollectionName description="Warehouse Items" languagecode="1033" />
                  </LocalizedCollectionNames>
                  <attributes>
                    <attribute PhysicalName="udpp_warehouseitemId">
                      <Type>primarykey</Type>
                      <LogicalName>udpp_warehouseitemid</LogicalName>
                      <RequiredLevel>systemrequired</RequiredLevel>
                      <ValidForReadApi>1</ValidForReadApi>
                      <displaynames><displayname description="Warehouse Item" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="udpp_name">
                      <Type>nvarchar</Type>
                      <LogicalName>udpp_name</LogicalName>
                      <RequiredLevel>required</RequiredLevel>
                      <DisplayMask>PrimaryName|ValidForAdvancedFind|ValidForForm|ValidForGrid</DisplayMask>
                      <MaxLength>100</MaxLength>
                      <ValidForCreateApi>1</ValidForCreateApi>
                      <ValidForUpdateApi>1</ValidForUpdateApi>
                      <IsCustomField>1</IsCustomField>
                      <displaynames><displayname description="Name" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="statecode">
                      <Type>state</Type>
                      <LogicalName>statecode</LogicalName>
                      <RequiredLevel>systemrequired</RequiredLevel>
                      <ValidForUpdateApi>1</ValidForUpdateApi>
                      <displaynames><displayname description="Status" languagecode="1033" /></displaynames>
                      <optionset Name="udpp_warehouseitem_statecode">
                        <displaynames><displayname description="Status" languagecode="1033" /></displaynames>
                        <states>
                          <state value="0"><labels><label description="Active" languagecode="1033" /></labels></state>
                          <state value="1"><labels><label description="Inactive" languagecode="1033" /></labels></state>
                        </states>
                      </optionset>
                    </attribute>
                    <attribute PhysicalName="udpp_grade">
                      <Type>picklist</Type>
                      <LogicalName>udpp_grade</LogicalName>
                      <RequiredLevel>none</RequiredLevel>
                      <ValidForCreateApi>1</ValidForCreateApi>
                      <ValidForUpdateApi>1</ValidForUpdateApi>
                      <IsCustomField>1</IsCustomField>
                      <OptionSetName>udpp_grade</OptionSetName>
                      <displaynames><displayname description="Grade" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="udpp_ParentId">
                      <Type>lookup</Type>
                      <LogicalName>udpp_parentid</LogicalName>
                      <RequiredLevel>none</RequiredLevel>
                      <ValidForCreateApi>1</ValidForCreateApi>
                      <ValidForUpdateApi>1</ValidForUpdateApi>
                      <IsCustomField>1</IsCustomField>
                      <displaynames><displayname description="Parent" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="CreatedBy">
                      <Type>lookup</Type>
                      <LogicalName>createdby</LogicalName>
                      <RequiredLevel>none</RequiredLevel>
                      <DisplayMask>ValidForAdvancedFind|ValidForGrid</DisplayMask>
                      <displaynames><displayname description="Created By" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="CreatedOn">
                      <Type>datetime</Type>
                      <LogicalName>createdon</LogicalName>
                      <RequiredLevel>none</RequiredLevel>
                      <displaynames><displayname description="Created On" languagecode="1033" /></displaynames>
                    </attribute>
                    <attribute PhysicalName="OwnerId">
                      <Type>owner</Type>
                      <LogicalName>ownerid</LogicalName>
                      <RequiredLevel>systemrequired</RequiredLevel>
                      <ValidForCreateApi>1</ValidForCreateApi>
                      <ValidForUpdateApi>1</ValidForUpdateApi>
                      <displaynames><displayname description="Owner" languagecode="1033" /></displaynames>
                    </attribute>
                  </attributes>
                </entity>
              </EntityInfo>
            </Entity>
            """);

        File.WriteAllText(Path.Combine(_model, "OptionSets", "udpp_grade.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <optionset Name="udpp_grade">
              <displaynames><displayname description="Grade" languagecode="1033" /></displaynames>
              <options>
                <option value="100000000"><labels><label description="Gold" languagecode="1033" /></labels></option>
                <option value="100000001"><labels><label description="Silver" languagecode="1033" /></labels></option>
              </options>
            </optionset>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string ServiceTemplatePath() =>
        Path.Combine(_app, "src", "generated", "services", "capitalizedentitylogicalnameexamplesService.ts");

    private AppCodeDataScaffoldRequest Request() => new()
    {
        AppProjectPath = _app,
        EntityLogicalName = "udpp_warehouseitem",
        ModelSolutionRootPath = _model,
        ServiceFilePath = ServiceTemplatePath(),
        IndexTemplateFilePath = Path.Combine(_app, "payload", "index.ts"),
        CommonModelsFilePath = Path.Combine(_app, "payload", "models", "CommonModels.ts"),
    };

    [Fact]
    public void Apply_BootstrapsGeneratedFolderAndFinalizesService()
    {
        AppCodeDataScaffold.Apply(Request());

        Assert.True(File.Exists(Path.Combine(_app, "src", "generated", "models", "CommonModels.ts")));
        var servicePath = Path.Combine(_app, "src", "generated", "services", "Udpp_warehouseitemsService.ts");
        Assert.True(File.Exists(servicePath));
        Assert.False(File.Exists(ServiceTemplatePath()));
        var content = File.ReadAllText(servicePath);
        Assert.Contains("class Udpp_warehouseitemsService", content);
        Assert.Contains("'udpp_warehouseitems'", content);
        Assert.DoesNotContain("example", content);
    }

    [Fact]
    public void Apply_GeneratesModelWithLfAndNoTrailingNewline()
    {
        AppCodeDataScaffold.Apply(Request());

        var modelPath = Path.Combine(_app, "src", "generated", "models", "Udpp_warehouseitemsModel.ts");
        var content = File.ReadAllText(modelPath);
        Assert.DoesNotContain("\r", content);
        Assert.False(content.EndsWith("\n"));
        Assert.Contains("export const Udpp_warehouseitemsstatecode = {", content);
        Assert.Contains("  0: 'Active',", content);
        Assert.Contains("export const Udpp_warehouseitemsudpp_grade = {", content);
        Assert.Contains("  100000000: 'Gold',", content);
        Assert.Contains("\"udpp_parentid@odata.bind\"?: string;", content);
        Assert.Contains("  udpp_name: string;", content);
        Assert.Contains("  createdon?: string;", content);
        Assert.Contains("  udpp_parentid?: object;", content);
        Assert.Contains("  _createdby_value?: string;", content);
        var stateIdx = content.IndexOf("statecode = {");
        var gradeIdx = content.IndexOf("udpp_grade = {");
        Assert.True(stateIdx < gradeIdx);
    }

    [Fact]
    public void Apply_InsertsIndexExportsWithCrlf()
    {
        AppCodeDataScaffold.Apply(Request());

        var content = File.ReadAllText(Path.Combine(_app, "src", "generated", "index.ts"));
        Assert.Contains("export * as Udpp_warehouseitemsModel from './models/Udpp_warehouseitemsModel';\r\n", content);
        Assert.Contains("export * from './services/Udpp_warehouseitemsService';\r\n", content);
        var modelsIdx = content.IndexOf("// Models");
        var insertIdx = content.IndexOf("export * as Udpp_warehouseitemsModel");
        var servicesIdx = content.IndexOf("// Services");
        Assert.True(modelsIdx < insertIdx && insertIdx < servicesIdx);
        Assert.True(content.EndsWith("\r\n"));
    }

    [Fact]
    public void Apply_AddsPowerConfigDataSourceIdempotently()
    {
        AppCodeDataScaffold.Apply(Request());
        var content = File.ReadAllText(Path.Combine(_app, "power.config.json"));
        Assert.Contains("\"default.cds\": {", content);
        Assert.Contains("\"warehouseitems\": {", content);
        Assert.Contains("\"entitySetName\": \"udpp_warehouseitems\"", content);
        Assert.Contains("\"logicalName\": \"udpp_warehouseitem\"", content);
        Assert.False(content.EndsWith("\n"));
        Assert.Contains("\r\n", content);

        PowerConfigFile.AddDataSource(Path.Combine(_app, "power.config.json"), "udpp_warehouseitem");
        var again = File.ReadAllText(Path.Combine(_app, "power.config.json"));
        Assert.Equal(content, again);
    }

    [Fact]
    public void Apply_CreatesDataSourcesInfoWithLfThenAppendsWithCrlf()
    {
        AppCodeDataScaffold.Apply(Request());

        var infoPath = Path.Combine(_app, ".power", "schemas", "appschemas", "dataSourcesInfo.ts");
        var created = File.ReadAllText(infoPath);
        Assert.DoesNotContain("\r", created);
        Assert.EndsWith("};\n", created);
        Assert.Contains("  \"udpp_warehouseitems\": {", created);
        Assert.Contains("    \"primaryKey\": \"udpp_warehouseitemid\",", created);

        var boxDir = Path.Combine(_model, "Entities", "udpp_box");
        Directory.CreateDirectory(boxDir);
        File.WriteAllText(Path.Combine(boxDir, "Entity.xml"), """
            <Entity><EntityInfo><entity Name="udpp_box">
              <EntitySetName>udpp_boxes</EntitySetName>
              <attributes>
                <attribute PhysicalName="udpp_boxId"><Type>primarykey</Type><LogicalName>udpp_boxid</LogicalName></attribute>
              </attributes>
            </entity></EntityInfo></Entity>
            """);
        DataSourcesInfoFile.AddEntry(infoPath, _model, "udpp_box");

        var appended = File.ReadAllText(infoPath);
        Assert.Contains("\r\n", appended);
        Assert.Contains("  },\r\n  \"udpp_boxes\": {", appended);
        Assert.Contains("    \"primaryKey\": \"udpp_boxid\",", appended);
    }

    [Fact]
    public void Apply_GeneratesSchemaJson()
    {
        AppCodeDataScaffold.Apply(Request());

        var schemaPath = Path.Combine(_app, ".power", "schemas", "dataverse", "warehouseitems.Schema.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(schemaPath))!;
        Assert.Equal("udpp_warehouseitem", json["name"]!.GetValue<string>());
        Assert.Equal("Warehouse Items", json["title"]!.GetValue<string>());
        var items = json["schema"]!["items"]!;
        Assert.Equal("udpp_warehouseitems", items["x-ms-dataverse-entityset"]!.GetValue<string>());
        Assert.Equal("udpp_warehouseitemid", items["x-ms-dataverse-primary-id"]!.GetValue<string>());
        Assert.Equal("udpp_name", items["x-ms-dataverse-primary-name"]!.GetValue<string>());

        var props = items["properties"]!.AsObject();
        Assert.Equal(100, props["udpp_name"]!["maxLength"]!.GetValue<int>());
        Assert.Equal("udpp_warehouseitemId", props["udpp_warehouseitemid"]!["x-ms-schema-name"]!.GetValue<string>());
        Assert.True(Guid.TryParse(props["statecode"]!["x-ms-optionsetmetadataid"]!.GetValue<string>(), out _));
        Assert.True(props["udpp_grade"]!["x-ms-isGlobal"]!.GetValue<bool>());
        Assert.Equal(new[] { "Gold", "Silver" }, props["udpp_grade"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.Equal("OwnerIdType", props["owneridtype"]!["x-ms-schema-name"]!.GetValue<string>());
        Assert.Equal("CreatedByName", props["createdbyname"]!["x-ms-schema-name"]!.GetValue<string>());
        Assert.Equal("VirtualType", props["statecodename"]!["x-ms-dataverse-type"]!.GetValue<string>());

        var required = items["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Contains("udpp_name", required);
        Assert.Contains("ownerid", required);
        Assert.Contains("owneridtype", required);
    }
}
