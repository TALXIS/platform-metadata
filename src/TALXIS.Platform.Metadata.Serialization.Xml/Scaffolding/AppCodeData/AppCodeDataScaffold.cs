using System.Text;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Initializes src/generated, finalizes the service file, generates the TypeScript
/// model and JSON schema from the model solution, and registers the data source
/// in index.ts, power.config.json and dataSourcesInfo.ts.
/// </summary>
public static class AppCodeDataScaffold
{
    private const string LowercaseToken = "lowercaseentitylogicalnameexample";
    private const string CapitalizedToken = "capitalizedentitylogicalnameexample";

    public static ScaffoldResult Apply(AppCodeDataScaffoldRequest request)
    {
        var entity = request.EntityLogicalName;
        // Capitalizes the first character of the entity logical name, rather than the entity set name.
        var capitalized = char.ToUpper(entity[0]) + entity.Substring(1);

        var generatedDir = Path.Combine(request.AppProjectPath, "src", "generated");
        var indexPath = Path.Combine(generatedDir, "index.ts");

        if (!File.Exists(indexPath))
        {
            Directory.CreateDirectory(Path.Combine(generatedDir, "models"));
            File.Copy(request.CommonModelsFilePath, Path.Combine(generatedDir, "models", "CommonModels.ts"), overwrite: true);
            File.Copy(request.IndexTemplateFilePath, indexPath, overwrite: true);
        }

        FinalizeServiceFile(request.ServiceFilePath, entity, capitalized);

        CodeAppModelGenerator.Generate(request.ModelSolutionRootPath, entity, Path.Combine(generatedDir, "models"));

        GeneratedIndexFile.InsertAfterTarget(indexPath, "// Models",
            $"export * as {capitalized}sModel from './models/{capitalized}sModel';");
        GeneratedIndexFile.InsertAfterTarget(indexPath, "// Services",
            $"export * from './services/{capitalized}sService';");

        PowerConfigFile.AddDataSource(Path.Combine(request.AppProjectPath, "power.config.json"), entity);

        DataSourcesInfoFile.AddEntry(
            Path.Combine(request.AppProjectPath, ".power", "schemas", "appschemas", "dataSourcesInfo.ts"),
            request.ModelSolutionRootPath, entity);

        CodeAppSchemaGenerator.Generate(request.ModelSolutionRootPath, entity,
            Path.Combine(request.AppProjectPath, ".power", "schemas", "dataverse"));

        return new ScaffoldResult();
    }

    private static void FinalizeServiceFile(string serviceFilePath, string entity, string capitalized)
    {
        if (!File.Exists(serviceFilePath))
            throw new FileNotFoundException($"Rendered service file not found: {serviceFilePath}");

        var content = File.ReadAllText(serviceFilePath);
        content = content.Replace(LowercaseToken, entity).Replace(CapitalizedToken, capitalized);
        File.WriteAllText(serviceFilePath, content, new UTF8Encoding(false));

        var fileName = Path.GetFileName(serviceFilePath);
        var newName = fileName.Replace(CapitalizedToken, capitalized);
        if (newName != fileName)
        {
            var newPath = Path.Combine(Path.GetDirectoryName(serviceFilePath)!, newName);
            if (File.Exists(newPath)) File.Delete(newPath);
            File.Move(serviceFilePath, newPath);
        }
    }
}
