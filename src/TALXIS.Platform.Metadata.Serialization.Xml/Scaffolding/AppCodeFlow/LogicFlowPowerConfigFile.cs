using System.Text.Json.Nodes;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the flow as a shared_logicflows connection reference in power.config.json.
/// </summary>
internal static class LogicFlowPowerConfigFile
{
    /// <summary>
    /// Adds or replaces the flow's reference and returns its data source name.
    /// </summary>
    public static string Register(string filePath, PowerAppsFlowDefinition flow, string schemaName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var powerConfig = JsonNode.Parse(File.ReadAllText(filePath))!.AsObject();
        if (powerConfig["connectionReferences"] is not JsonObject connectionReferences)
        {
            connectionReferences = new JsonObject();
            powerConfig["connectionReferences"] = connectionReferences;
        }

        var referenceKey = connectionReferences
            .FirstOrDefault(r => string.Equals((string?)r.Value?["workflowDetails"]?["workflowEntityId"], flow.WorkflowId, StringComparison.OrdinalIgnoreCase))
            .Key ?? Guid.NewGuid().ToString();

        var takenDataSources = new HashSet<string>(connectionReferences
            .Where(r => r.Key != referenceKey)
            .SelectMany(r => (r.Value?["dataSources"] as JsonArray)?.Select(d => ((string)d!).ToLowerInvariant()) ?? Enumerable.Empty<string>()));
        var dataSourceName = schemaName.ToLowerInvariant();
        for (var i = 1; takenDataSources.Contains(dataSourceName); i++) dataSourceName = $"{schemaName.ToLowerInvariant()}_{i}";

        // workflowName is the Power Automate flow id, which only exists in an environment; the workflow id stands in for it offline.
        connectionReferences[referenceKey] = new JsonObject
        {
            ["id"] = LogicFlowCodegen.LogicFlowsApiId,
            ["displayName"] = "Logic flows",
            ["dataSources"] = new JsonArray(dataSourceName),
            ["workflowDetails"] = new JsonObject
            {
                ["workflowEntityId"] = flow.WorkflowId,
                ["workflowDisplayName"] = flow.DisplayName,
                ["workflowName"] = flow.WorkflowId,
            },
        };
        LogicFlowCodegen.WriteText(filePath, powerConfig.ToJsonString(LogicFlowCodegen.JsonOptions) + "\n");
        return dataSourceName;
    }
}
