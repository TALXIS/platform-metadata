namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Adds a Power Apps (V2) triggered cloud flow to a code app offline, writing the same files
/// 'pa app add flow' does: power.config.json reference, logicflows schema, dataSourcesInfo entry,
/// typed model and service with Run(). Re-running rewrites them in place.
/// </summary>
public static class AppCodeFlowScaffold
{
    public static ScaffoldResult Apply(AppCodeFlowScaffoldRequest request)
    {
        var result = new ScaffoldResult();
        var appPath = request.AppProjectPath;
        var solutionPath = Path.GetFullPath(Path.Combine(appPath, request.FlowSolutionPath));

        var flow = PowerAppsFlowDefinition.Load(solutionPath, request.FlowName);
        if (flow.InvokerConnections.Count > 0)
            result.AddWarning($"Run-only user connections ({string.Join(", ", flow.InvokerConnections)}) are not wired into power.config.json dependencies.");

        // Same naming rules as the pa CLI codegen.
        var schemaName = TypeScriptIdentifier.From(flow.DisplayName, stripWhitespace: true);
        var modelName = char.ToUpperInvariant(schemaName[0]) + schemaName.Substring(1);

        var dataSourceName = LogicFlowPowerConfigFile.Register(Path.Combine(appPath, "power.config.json"), flow, schemaName);
        LogicFlowSchemaFile.Write(Path.Combine(appPath, ".power", "schemas", "logicflows", $"{schemaName}.Schema.json"), flow);
        LogicFlowDataSourcesInfoFile.AddEntry(Path.Combine(appPath, ".power", "schemas", "appschemas", "dataSourcesInfo.ts"), dataSourceName, flow);

        var generatedDir = Path.Combine(appPath, "src", "generated");
        var indexPath = Path.Combine(generatedDir, "index.ts");
        // Same bootstrap as pp-app-code-data, so later data sources still find CommonModels.
        if (!File.Exists(indexPath))
        {
            Directory.CreateDirectory(Path.Combine(generatedDir, "models"));
            File.Copy(request.CommonModelsFilePath, Path.Combine(generatedDir, "models", "CommonModels.ts"), overwrite: true);
            File.Copy(request.IndexTemplateFilePath, indexPath, overwrite: true);
        }

        LogicFlowTypeScriptFiles.Write(generatedDir, modelName, dataSourceName, flow);
        return result;
    }
}
