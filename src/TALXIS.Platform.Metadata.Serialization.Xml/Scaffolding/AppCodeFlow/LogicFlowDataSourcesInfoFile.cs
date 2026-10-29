using System.Text.Json.Nodes;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Adds or replaces the flow's Connector entry in .power/schemas/appschemas/dataSourcesInfo.ts.
/// </summary>
internal static class LogicFlowDataSourcesInfoFile
{
    public static void AddEntry(string filePath, string dataSourceName, PowerAppsFlowDefinition flow)
    {
        var dataSourcesInfo = new JsonObject();
        if (File.Exists(filePath))
        {
            var text = File.ReadAllText(filePath);
            var start = text.IndexOf('{', text.IndexOf("dataSourcesInfo", StringComparison.Ordinal));
            var end = text.LastIndexOf('}');
            dataSourcesInfo = JsonNode.Parse(text.Substring(start, end - start + 1))!.AsObject();
        }

        var responseInfo = new JsonObject();
        if (flow.ResponseSchema is not null)
        {
            responseInfo["200"] = new JsonObject { ["type"] = "object" };
            responseInfo["default"] = new JsonObject { ["type"] = "object" };
        }
        else
        {
            responseInfo["202"] = new JsonObject { ["type"] = "void" };
        }

        dataSourcesInfo[dataSourceName] = new JsonObject
        {
            ["tableId"] = "",
            ["version"] = "",
            ["primaryKey"] = "",
            ["dataSourceType"] = "Connector",
            ["apis"] = new JsonObject
            {
                ["Run"] = new JsonObject
                {
                    ["path"] = flow.RunPath,
                    ["method"] = "POST",
                    ["parameters"] = new JsonArray(
                        new JsonObject { ["name"] = "connectionId", ["in"] = "path", ["required"] = true, ["type"] = "string" },
                        new JsonObject { ["name"] = "input", ["in"] = "body", ["required"] = true, ["type"] = "object" },
                        new JsonObject { ["name"] = "api-version", ["in"] = "query", ["required"] = true, ["type"] = "string" }),
                    ["responseInfo"] = responseInfo,
                },
            },
        };
        LogicFlowCodegen.WriteText(filePath,
            "/*!\n * Copyright (C) Microsoft Corporation. All rights reserved.\n * This file is auto-generated. Do not modify it manually.\n * Changes to this file may be overwritten.\n */\n\n" +
            $"export const dataSourcesInfo = {dataSourcesInfo.ToJsonString(LogicFlowCodegen.JsonOptions)};\n");
    }
}
