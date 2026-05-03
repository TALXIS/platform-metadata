using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

internal static class FlowDefinitionReader
{
    public static void Load(Workspace workspace, string rootPath)
    {
        var workflowsDir = Path.Combine(rootPath, "Workflows");
        if (!Directory.Exists(workflowsDir)) return;

        foreach (var file in Directory.EnumerateFiles(workflowsDir, "*.json", SearchOption.AllDirectories))
        {
            JObject root;
            try
            {
                root = LoadJsonDocument(file);
            }
            catch (JsonReaderException ex)
            {
                workspace.AddLoadError(
                    file,
                    $"Invalid JSON: {ex.Message}",
                    ex.LineNumber > 0 ? ex.LineNumber : null,
                    ex.LineNumber > 0 ? Math.Max(ex.LinePosition, 1) : null);
                continue;
            }

            var flow = Parse(rootPath, file, root);
            workspace.AddFlowDefinition(flow);
            AttachFlowDefinition(workspace, flow, file);
        }
    }

    private static FlowDefinitionMetadata Parse(string rootPath, string filePath, JObject root)
    {
        var flow = new FlowDefinitionMetadata
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = GetRelativePath(rootPath, filePath),
            SchemaVersion = root["schemaVersion"]?.Value<string>(),
            RawJson = root.ToString(Formatting.None),
            Source = CreateSourceLocation(filePath, root)
        };

        var properties = root["properties"] as JObject;
        if (properties == null)
        {
            AddDiagnostic(flow, filePath, "FLOW001", FlowDiagnosticSeverity.Error,
                "Flow JSON must contain a properties object.", root);
        }

        AddConnectionReferences(flow, filePath, properties?["connectionReferences"]);
        AddDefinitionProjection(flow, filePath, properties?["definition"], properties ?? root);

        return flow;
    }

    private static void AddConnectionReferences(FlowDefinitionMetadata flow, string filePath, JToken? token)
    {
        if (token != null && token is not JObject)
        {
            AddDiagnostic(flow, filePath, "FLOW002", FlowDiagnosticSeverity.Error,
                "Flow properties.connectionReferences must be an object.", token);
            return;
        }

        if (token is not JObject connectionReferences) return;

        foreach (var property in connectionReferences.Properties())
        {
            if (property.Value is not JObject reference)
            {
                AddDiagnostic(flow, filePath, "FLOW003", FlowDiagnosticSeverity.Error,
                    $"Connection reference '{property.Name}' must be an object.", property, property.Name);
                continue;
            }

            flow.AddConnectionReference(new FlowConnectionReferenceMetadata
            {
                Name = property.Name,
                ApiId = reference.SelectToken("api.id")?.Value<string>(),
                ConnectionName = reference.SelectToken("connectionName")?.Value<string>()
                    ?? reference.SelectToken("connection.name")?.Value<string>(),
                ConnectionReferenceLogicalName = reference.SelectToken("connection.connectionReferenceLogicalName")?.Value<string>()
                    ?? reference.SelectToken("connectionReferenceLogicalName")?.Value<string>(),
                Source = CreateSourceLocation(filePath, property)
            });
        }
    }

    private static void AddDefinitionProjection(FlowDefinitionMetadata flow, string filePath, JToken? token, JToken fallbackSource)
    {
        if (token == null)
        {
            AddDiagnostic(flow, filePath, "FLOW004", FlowDiagnosticSeverity.Error,
                "Flow JSON must contain properties.definition.", fallbackSource);
            return;
        }

        if (token is not JObject definition)
        {
            AddDiagnostic(flow, filePath, "FLOW004", FlowDiagnosticSeverity.Error,
                "Flow properties.definition must be an object.", token);
            return;
        }

        flow.FlowSchema = definition["$schema"]?.Value<string>();
        flow.ContentVersion = definition["contentVersion"]?.Value<string>();
        AddRootNodes(flow, filePath, definition, "triggers", "trigger");
        AddRootNodes(flow, filePath, definition, "actions", "action");
    }

    private static void AddRootNodes(FlowDefinitionMetadata flow, string filePath, JObject definition, string propertyName, string kind)
    {
        var token = definition[propertyName];
        if (token == null)
        {
            AddDiagnostic(flow, filePath, "FLOW005", FlowDiagnosticSeverity.Warning,
                $"Flow definition does not contain a {propertyName} object.", definition);
            return;
        }

        if (token is not JObject container)
        {
            AddDiagnostic(flow, filePath, "FLOW005", FlowDiagnosticSeverity.Error,
                $"Flow definition {propertyName} value must be an object.", token);
            return;
        }

        AddNodes(flow, filePath, container, kind, null, propertyName, null);
    }

    private static void AddNodes(
        FlowDefinitionMetadata flow,
        string filePath,
        JObject container,
        string kind,
        FlowNodeMetadata? parent,
        string containerPath,
        string? branchName)
    {
        var siblingNames = new HashSet<string>(
            container.Properties().Where(p => p.Value is JObject).Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var property in container.Properties())
        {
            if (property.Value is not JObject nodeObject)
            {
                AddDiagnostic(flow, filePath, "FLOW006", FlowDiagnosticSeverity.Error,
                    $"{kind} '{property.Name}' must be an object.", property, property.Name);
                continue;
            }

            var node = new FlowNodeMetadata
            {
                Name = property.Name,
                Kind = kind,
                Type = nodeObject["type"]?.Value<string>(),
                OperationId = nodeObject.SelectToken("inputs.host.operationId")?.Value<string>() ?? nodeObject["operationId"]?.Value<string>(),
                JsonPath = property.Path,
                ParentPath = parent?.JsonPath,
                ContainerPath = containerPath,
                BranchName = branchName,
                Source = CreateSourceLocation(filePath, property)
            };

            AddRunAfterDependencies(flow, filePath, node, nodeObject["runAfter"], siblingNames);
            AddConnectionReferenceUsage(flow, filePath, node, nodeObject);
            AddExpressionReferences(filePath, node, nodeObject);

            if (string.Equals(kind, "trigger", StringComparison.Ordinal))
                flow.AddTrigger(node);
            else if (parent == null)
                flow.AddAction(node);
            else
                parent.AddChild(node);

            if (string.Equals(kind, "action", StringComparison.Ordinal))
                AddNestedActionContainers(flow, filePath, node, nodeObject);
        }
    }

    private static void AddNestedActionContainers(FlowDefinitionMetadata flow, string filePath, FlowNodeMetadata parent, JObject node)
    {
        AddOptionalActionContainer(flow, filePath, parent, node, "actions", "actions", null);

        if (node["else"] is JObject elseNode)
            AddOptionalActionContainer(flow, filePath, parent, elseNode, "actions", "else.actions", "else");
        else if (node["else"] != null)
            AddDiagnostic(flow, filePath, "FLOW007", FlowDiagnosticSeverity.Error,
                $"Action '{parent.Name}' has an else value that must be an object.", node["else"], parent.Name);

        if (node["cases"] is JObject cases)
        {
            foreach (var caseProperty in cases.Properties())
            {
                if (caseProperty.Value is not JObject caseNode)
                {
                    AddDiagnostic(flow, filePath, "FLOW007", FlowDiagnosticSeverity.Error,
                        $"Switch case '{caseProperty.Name}' must be an object.", caseProperty, caseProperty.Name);
                    continue;
                }

                AddOptionalActionContainer(flow, filePath, parent, caseNode, "actions", $"cases.{caseProperty.Name}.actions", caseProperty.Name);
            }
        }
        else if (node["cases"] != null)
        {
            AddDiagnostic(flow, filePath, "FLOW007", FlowDiagnosticSeverity.Error,
                $"Action '{parent.Name}' has a cases value that must be an object.", node["cases"], parent.Name);
        }

        if (node["default"] is JObject defaultNode)
            AddOptionalActionContainer(flow, filePath, parent, defaultNode, "actions", "default.actions", "default");
        else if (node["default"] != null)
            AddDiagnostic(flow, filePath, "FLOW007", FlowDiagnosticSeverity.Error,
                $"Action '{parent.Name}' has a default value that must be an object.", node["default"], parent.Name);
    }

    private static void AddOptionalActionContainer(
        FlowDefinitionMetadata flow,
        string filePath,
        FlowNodeMetadata parent,
        JObject owner,
        string propertyName,
        string containerPath,
        string? branchName)
    {
        var token = owner[propertyName];
        if (token == null) return;

        if (token is not JObject container)
        {
            AddDiagnostic(flow, filePath, "FLOW007", FlowDiagnosticSeverity.Error,
                $"Action '{parent.Name}' has a {containerPath} value that must be an object.", token, parent.Name);
            return;
        }

        AddNodes(flow, filePath, container, "action", parent, containerPath, branchName);
    }

    private static void AddRunAfterDependencies(
        FlowDefinitionMetadata flow,
        string filePath,
        FlowNodeMetadata node,
        JToken? runAfterToken,
        HashSet<string> siblingNames)
    {
        if (runAfterToken == null) return;

        if (runAfterToken is not JObject runAfter)
        {
            AddDiagnostic(flow, filePath, "FLOW008", FlowDiagnosticSeverity.Error,
                $"Action '{node.Name}' has a runAfter value that must be an object.", runAfterToken, node.Name);
            return;
        }

        foreach (var dependencyProperty in runAfter.Properties())
        {
            IReadOnlyList<string> statuses = dependencyProperty.Value is JArray statusArray
                ? statusArray.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToArray()
                : Array.Empty<string>();

            node.AddRunAfter(new FlowRunAfterDependency
            {
                TargetName = dependencyProperty.Name,
                Statuses = statuses,
                JsonPath = dependencyProperty.Path
            });

            if (!siblingNames.Contains(dependencyProperty.Name))
            {
                AddDiagnostic(flow, filePath, "FLOW009", FlowDiagnosticSeverity.Error,
                    $"Action '{node.Name}' has a runAfter dependency on unknown sibling action '{dependencyProperty.Name}'.",
                    dependencyProperty,
                    dependencyProperty.Name);
            }
        }
    }

    private static void AddConnectionReferenceUsage(FlowDefinitionMetadata flow, string filePath, FlowNodeMetadata node, JObject nodeObject)
    {
        var connectionReferenceName = nodeObject.SelectToken("inputs.host.connectionReferenceName")?.Value<string>();
        if (string.IsNullOrWhiteSpace(connectionReferenceName)) return;

        node.AddConnectionReferenceName(connectionReferenceName!);

        if (!flow.ConnectionReferences.Any(r => string.Equals(r.Name, connectionReferenceName, StringComparison.OrdinalIgnoreCase)))
        {
            AddDiagnostic(flow, filePath, "FLOW010", FlowDiagnosticSeverity.Error,
                $"Action '{node.Name}' uses unknown connection reference '{connectionReferenceName}'.",
                nodeObject.SelectToken("inputs.host.connectionReferenceName") ?? nodeObject,
                connectionReferenceName);
        }
    }

    private static void AddExpressionReferences(string filePath, FlowNodeMetadata node, JObject nodeObject)
    {
        foreach (var value in nodeObject.DescendantsAndSelf().OfType<JValue>())
        {
            if (value.Type != JTokenType.String || value.Value is not string expression)
                continue;

            if (!expression.Contains("@") && expression.IndexOf("parameters(", StringComparison.Ordinal) < 0 && expression.IndexOf("outputs(", StringComparison.Ordinal) < 0)
                continue;

            foreach (var reference in ExtractExpressionReferences(expression, value.Path, CreateSourceLocation(filePath, value)))
                node.AddExpressionReference(reference);
        }
    }

    private static IEnumerable<FlowExpressionReference> ExtractExpressionReferences(string expression, string jsonPath, SourceLocation source)
    {
        foreach (Match match in Regex.Matches(expression, @"(?<kind>parameters|outputs|body|variables|items|environment)\('(?<name>[^']+)'\)"))
        {
            yield return new FlowExpressionReference
            {
                Kind = match.Groups["kind"].Value,
                Name = match.Groups["name"].Value,
                Expression = expression,
                JsonPath = jsonPath,
                Source = source
            };
        }

        foreach (Match match in Regex.Matches(expression, @"(?<kind>triggerOutputs|triggerBody)\(\)"))
        {
            yield return new FlowExpressionReference
            {
                Kind = match.Groups["kind"].Value,
                Expression = expression,
                JsonPath = jsonPath,
                Source = source
            };
        }
    }

    private static void AddDiagnostic(
        FlowDefinitionMetadata flow,
        string filePath,
        string code,
        FlowDiagnosticSeverity severity,
        string message,
        JToken? source,
        string? relatedName = null)
    {
        var location = CreateSourceLocation(filePath, source);
        flow.AddDiagnostic(new FlowDiagnostic
        {
            Severity = severity,
            Code = code,
            Message = message,
            FilePath = filePath,
            JsonPath = source?.Path,
            Line = location.Line,
            Column = location.Column,
            RelatedName = relatedName
        });
    }

    private static void AttachFlowDefinition(Workspace workspace, FlowDefinitionMetadata flow, string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var workflow = workspace.Workflows.FirstOrDefault(w =>
            string.Equals(w.UniqueName, fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(w.JsonFileName), fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(w.JsonFileName), Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(w.XamlFileName), fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(w.XamlFileName), Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

        if (workflow != null)
            workflow.FlowDefinition = flow;
    }

    private static JObject LoadJsonDocument(string filePath)
    {
        using var textReader = File.OpenText(filePath);
        using var jsonReader = new JsonTextReader(textReader)
        {
            DateParseHandling = DateParseHandling.None
        };

        return JObject.Load(jsonReader, new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load
        });
    }

    private static SourceLocation CreateSourceLocation(string filePath, JToken? source)
    {
        if (source is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
            return new SourceLocation(filePath, lineInfo.LineNumber, lineInfo.LinePosition);

        return new SourceLocation(filePath, 1, 1);
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        return filePath.Substring(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
    }
}
