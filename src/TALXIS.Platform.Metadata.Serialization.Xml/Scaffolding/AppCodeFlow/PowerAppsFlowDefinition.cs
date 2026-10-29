using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Power Apps (V2) triggered flow read from solution source (Workflows/&lt;name&gt;-&lt;GUID&gt;.json and its .data.xml).
/// </summary>
internal sealed class PowerAppsFlowDefinition
{
    public string WorkflowId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string RunPath { get; private set; } = "";
    public JsonObject InputSchema { get; private set; } = new();
    public JsonObject? ResponseSchema { get; private set; }
    public IReadOnlyList<string> InvokerConnections { get; private set; } = Array.Empty<string>();

    public static PowerAppsFlowDefinition Load(string solutionPath, string flowName)
    {
        // Searched recursively, so it works whatever SolutionRootPath the solution project uses.
        var flowJsonPath = Directory.Exists(solutionPath)
            ? Directory.GetFiles(solutionPath, $"{flowName}-*.json", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Workflows"
                    && !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
            : null;
        if (flowJsonPath is null)
            throw new FileNotFoundException($"Flow definition 'Workflows/{flowName}-<GUID>.json' not found under {solutionPath}");

        var dataXmlPath = flowJsonPath + ".data.xml";
        if (!File.Exists(dataXmlPath))
            throw new FileNotFoundException($"Flow metadata not found: {dataXmlPath}");

        var workflowXml = XDocument.Load(dataXmlPath).Root!;
        var workflowId = workflowXml.Attribute("WorkflowId")?.Value.Trim('{', '}').ToLowerInvariant();
        var displayName = workflowXml.Attribute("Name")?.Value ?? flowName;
        if (string.IsNullOrEmpty(workflowId))
            throw new InvalidOperationException($"WorkflowId missing in {dataXmlPath}");

        var flow = JsonNode.Parse(File.ReadAllText(flowJsonPath));
        var definition = flow?["properties"]?["definition"];

        var trigger = (definition?["triggers"] as JsonObject)?
            .FirstOrDefault(t => (string?)t.Value?["kind"] == "PowerAppV2");
        if (trigger?.Value is null)
            throw new InvalidOperationException($"Flow '{displayName}' has no Power Apps (V2) trigger. Only flows with a PowerAppV2 trigger can be called from a code app.");

        var invokerConnections = (flow?["properties"]?["connectionReferences"] as JsonObject)?
            .Where(c => string.Equals((string?)c.Value?["runtimeSource"], "invoker", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Key)
            .ToList() ?? new List<string>();

        return new PowerAppsFlowDefinition
        {
            WorkflowId = workflowId!,
            DisplayName = displayName,
            RunPath = $"/{{connectionId}}/triggers/{trigger.Value.Key}/run",
            InputSchema = trigger.Value.Value["inputs"]?["schema"]?.DeepClone() as JsonObject
                ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["required"] = new JsonArray() },
            ResponseSchema = FindResponseSchema(definition?["actions"] as JsonObject),
            InvokerConnections = invokerConnections,
        };
    }

    private static JsonObject? FindResponseSchema(JsonObject? actions)
    {
        if (actions is null) return null;
        foreach (var entry in actions)
        {
            if (entry.Value is not JsonObject a) continue;
            if ((string?)a["type"] == "Response" && (string?)a["kind"] == "PowerApp" && a["inputs"]?["schema"] is JsonObject s) return (JsonObject)s.DeepClone();

            var nested = new[] { a["actions"], a["else"]?["actions"], a["default"]?["actions"] }
                .Concat((a["cases"] as JsonObject)?.Select(c => c.Value?["actions"]) ?? Enumerable.Empty<JsonNode?>());
            foreach (var n in nested)
            {
                var found = FindResponseSchema(n as JsonObject);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
