using System.Text.Json.Nodes;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Writes .power/schemas/logicflows/&lt;name&gt;.Schema.json, the swagger of the flow's Run operation.
/// </summary>
internal static class LogicFlowSchemaFile
{
    public static void Write(string filePath, PowerAppsFlowDefinition flow)
    {
        var definitions = new JsonObject { ["ManualTriggerInput"] = flow.InputSchema.DeepClone() };
        var responses = new JsonObject();
        if (flow.ResponseSchema is not null)
        {
            definitions["ResponseActionOutput"] = flow.ResponseSchema.DeepClone();
            responses["200"] = new JsonObject { ["description"] = "Ok", ["schema"] = new JsonObject { ["$ref"] = "#/definitions/ResponseActionOutput" } };
            responses["default"] = new JsonObject { ["description"] = "The Flow Response.", ["schema"] = new JsonObject { ["type"] = "object" } };
        }
        else
        {
            responses["202"] = new JsonObject { ["description"] = "Accepted" };
        }

        var schema = new JsonObject
        {
            ["name"] = flow.DisplayName,
            ["id"] = LogicFlowCodegen.LogicFlowsApiId,
            ["type"] = "Microsoft.PowerApps/apis",
            ["properties"] = new JsonObject
            {
                ["displayName"] = flow.DisplayName,
                ["isCustom"] = false,
                ["swagger"] = new JsonObject
                {
                    ["swagger"] = "2.0",
                    ["info"] = new JsonObject { ["title"] = flow.DisplayName, ["version"] = LogicFlowCodegen.ApiVersion },
                    ["schemes"] = new JsonArray("https"),
                    ["consumes"] = new JsonArray("application/json"),
                    ["produces"] = new JsonArray("application/json"),
                    ["paths"] = new JsonObject
                    {
                        [flow.RunPath] = new JsonObject
                        {
                            ["post"] = new JsonObject
                            {
                                ["operationId"] = "Run",
                                ["description"] = "Runs a logic flow trigger.",
                                ["parameters"] = new JsonArray(
                                    new JsonObject { ["name"] = "connectionId", ["in"] = "path", ["required"] = true, ["type"] = "string", ["x-ms-visibility"] = "internal" },
                                    new JsonObject { ["name"] = "input", ["description"] = "The trigger's input", ["in"] = "body", ["required"] = true, ["schema"] = new JsonObject { ["$ref"] = "#/definitions/ManualTriggerInput" } },
                                    new JsonObject { ["name"] = "api-version", ["description"] = "The api version", ["in"] = "query", ["required"] = true, ["default"] = LogicFlowCodegen.ApiVersion, ["x-ms-visibility"] = "internal", ["type"] = "string" }),
                                ["responses"] = responses,
                            },
                        },
                    },
                    ["definitions"] = definitions,
                    ["basePath"] = "/apim/logicflows",
                },
            },
        };
        LogicFlowCodegen.WriteText(filePath, schema.ToJsonString(LogicFlowCodegen.JsonOptions));
    }
}
