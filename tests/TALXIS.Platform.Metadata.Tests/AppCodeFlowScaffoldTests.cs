using System.Text.Json.Nodes;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class AppCodeFlowScaffoldTests : IDisposable
{
    private const string WorkflowId = "6f2a1c3e-0000-4000-8000-0000000000aa";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-appcodeflow").FullName;
    private readonly string _app;
    private readonly string _workflows;

    public AppCodeFlowScaffoldTests()
    {
        _app = Path.Combine(_root, "Apps.Tester");
        _workflows = Path.Combine(_root, "Solutions.Logic", "Declarations", "Workflows");
        Directory.CreateDirectory(Path.Combine(_app, "payload", "models"));
        Directory.CreateDirectory(_workflows);

        File.WriteAllText(Path.Combine(_app, "power.config.json"), """
            {
              "appId": "",
              "connectionReferences": {},
              "databaseReferences": {}
            }
            """);
        File.WriteAllText(Path.Combine(_app, "payload", "index.ts"),
            "/*!\n * Header\n */\n\n// Models\nexport * as CommonModels from './models/CommonModels';\n\n\n// Services\n");
        File.WriteAllText(Path.Combine(_app, "payload", "models", "CommonModels.ts"), "export interface IGetOptions {}\n");

        WriteFlow(withResponse: true);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Apply_WritesModelServiceSchemaAndRegistrations()
    {
        Apply();

        var model = Read("src", "generated", "models", "RestockRequestModel.ts");
        Assert.Contains("export interface ManualTriggerInput {\n  // Item to restock\n  text: string;\n  number?: number;\n}\n", model);
        Assert.Contains("export interface ResponseActionOutput {\n  status?: 'ok' | 'failed';\n}\n", model);

        var service = Read("src", "generated", "services", "RestockRequestService.ts");
        Assert.Contains("private static readonly dataSourceName = 'restockrequest';", service);
        Assert.Contains("Promise<IOperationResult<ResponseActionOutput>>", service);

        var index = Read("src", "generated", "index.ts");
        Assert.Contains("export * as CommonModels from './models/CommonModels';\nexport * as RestockRequestModel from './models/RestockRequestModel';\n", index);
        Assert.Contains("// Services\nexport * from './services/RestockRequestService';\n", index);
        Assert.True(File.Exists(Path.Combine(_app, "src", "generated", "models", "CommonModels.ts")));

        var schema = JsonNode.Parse(Read(".power", "schemas", "logicflows", "RestockRequest.Schema.json"))!;
        var post = schema["properties"]!["swagger"]!["paths"]!["/{connectionId}/triggers/manual/run"]!["post"]!;
        Assert.Equal("#/definitions/ResponseActionOutput", (string?)post["responses"]!["200"]!["schema"]!["$ref"]);

        var dataSourcesInfo = Read(".power", "schemas", "appschemas", "dataSourcesInfo.ts");
        Assert.Contains("\"restockrequest\": {", dataSourcesInfo);
        Assert.Contains("\"dataSourceType\": \"Connector\"", dataSourcesInfo);

        var reference = SingleLogicFlowReference();
        Assert.Equal(WorkflowId, (string?)reference["workflowDetails"]!["workflowEntityId"]);
        Assert.Equal("Restock Request", (string?)reference["workflowDetails"]!["workflowDisplayName"]);
        Assert.Equal("restockrequest", (string?)reference["dataSources"]![0]);
        Assert.DoesNotContain("\r\n", Read("power.config.json"));
    }

    [Fact]
    public void Apply_Twice_RewritesInPlace()
    {
        Apply();
        var key = SingleLogicFlowReferenceKey();
        Apply();

        Assert.Equal(key, SingleLogicFlowReferenceKey());
        var index = Read("src", "generated", "index.ts");
        Assert.Single(index.Split('\n'), l => l == "export * from './services/RestockRequestService';");
    }

    [Fact]
    public void Apply_WithoutResponseAction_ReturnsVoid()
    {
        WriteFlow(withResponse: false);

        Apply();

        var service = Read("src", "generated", "services", "RestockRequestService.ts");
        Assert.Contains("Promise<IOperationResult<void>>", service);
        Assert.DoesNotContain("ResponseActionOutput", Read("src", "generated", "models", "RestockRequestModel.ts"));
        Assert.Contains("\"202\": {\n            \"type\": \"void\"", Read(".power", "schemas", "appschemas", "dataSourcesInfo.ts"));
    }

    [Fact]
    public void Apply_KeepsExistingDataSourcesInfoEntries()
    {
        var path = Path.Combine(_app, ".power", "schemas", "appschemas", "dataSourcesInfo.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "/*!\n * Header\n */\n\nexport const dataSourcesInfo = {\n  \"lab_items\": {\n    \"dataSourceType\": \"Dataverse\",\n    \"apis\": {}\n  }\n};\n");

        Apply();

        var text = File.ReadAllText(path);
        Assert.Contains("\"lab_items\": {", text);
        Assert.Contains("\"restockrequest\": {", text);
    }

    [Fact]
    public void Apply_WithoutPowerAppsTrigger_Throws()
    {
        var flowPath = Directory.GetFiles(_workflows, "*.json").Single();
        File.WriteAllText(flowPath, File.ReadAllText(flowPath).Replace("PowerAppV2", "Http"));

        var ex = Assert.Throws<InvalidOperationException>(Apply);
        Assert.Contains("no Power Apps (V2) trigger", ex.Message);
    }

    private void Apply() => AppCodeFlowScaffold.Apply(new AppCodeFlowScaffoldRequest
    {
        AppProjectPath = _app,
        FlowSolutionPath = "../Solutions.Logic",
        FlowName = "lab_restockrequest",
        IndexTemplateFilePath = Path.Combine(_app, "payload", "index.ts"),
        CommonModelsFilePath = Path.Combine(_app, "payload", "models", "CommonModels.ts"),
    });

    private void WriteFlow(bool withResponse)
    {
        var actions = withResponse
            ? """
              "Condition": {
                "type": "If",
                "actions": {
                  "Respond": {
                    "type": "Response",
                    "kind": "PowerApp",
                    "inputs": { "schema": { "type": "object", "properties": { "status": { "type": "string", "enum": ["ok", "failed"] } } } }
                  }
                }
              }
              """
            : "";
        File.WriteAllText(Path.Combine(_workflows, $"lab_restockrequest-{WorkflowId.ToUpperInvariant()}.json"), $$"""
            {
              "properties": {
                "connectionReferences": {},
                "definition": {
                  "triggers": {
                    "manual": {
                      "type": "Request",
                      "kind": "PowerAppV2",
                      "inputs": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "text": { "type": "string", "description": "Item to restock" },
                            "number": { "type": "number" }
                          },
                          "required": ["text"]
                        }
                      }
                    }
                  },
                  "actions": { {{actions}} }
                }
              }
            }
            """);
        File.WriteAllText(Path.Combine(_workflows, $"lab_restockrequest-{WorkflowId.ToUpperInvariant()}.json.data.xml"),
            $"<Workflow WorkflowId=\"{{{WorkflowId.ToUpperInvariant()}}}\" Name=\"Restock Request\" />");
    }

    private string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { _app }.Concat(parts).ToArray()));

    private JsonObject ConnectionReferences() =>
        JsonNode.Parse(Read("power.config.json"))!["connectionReferences"]!.AsObject();

    private string SingleLogicFlowReferenceKey() => Assert.Single(ConnectionReferences()).Key;

    private JsonNode SingleLogicFlowReference() => Assert.Single(ConnectionReferences()).Value!;
}
