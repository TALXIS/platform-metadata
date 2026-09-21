using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Finalizes and registers the step in this order: filtering attributes, type-92
/// RootComponent, public key token, plugin type id, then copying the payload
/// into SdkMessageProcessingSteps/.
/// </summary>
public static class PluginAssemblyStepScaffold
{
    public static ScaffoldResult Apply(PluginAssemblyStepScaffoldRequest request)
    {
        ApplyFilteringAttributes(request.StepFilePath, request.FilteringAttributes);

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.SdkMessageProcessingStep,
            Id = Guid.Parse(request.StepId),
            Behavior = 0,
        });

        var assemblyDataXmlPath = FindAssemblyDataXml(request.SolutionRootPath, request.AssemblyName);
        ApplyPublicKeyToken(request.StepFilePath, assemblyDataXmlPath);
        ApplyPluginTypeId(request.StepFilePath, assemblyDataXmlPath, request.PluginClassName);

        var stepsDir = Path.Combine(request.SolutionRootPath, "SdkMessageProcessingSteps");
        Directory.CreateDirectory(stepsDir);
        File.Copy(request.StepFilePath, Path.Combine(stepsDir, $"{{{request.StepId}}}.xml"), overwrite: true);

        return new ScaffoldResult();
    }

    private static void ApplyFilteringAttributes(string stepFilePath, string spec)
    {
        var cleaned = spec.Trim('{', '}').Replace(" ", "").Replace("\"", "");
        var attributes = cleaned.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();

        var doc = new XmlDocument();
        doc.Load(stepFilePath);
        var filteringNode = doc.SelectSingleNode("//FilteringAttributes");
        if (filteringNode != null)
            filteringNode.InnerText = string.Join(",", attributes);
        doc.Save(stepFilePath);
    }

    private static string FindAssemblyDataXml(string solutionRootPath, string assemblyName)
    {
        var assembliesDir = Path.Combine(solutionRootPath, "PluginAssemblies");
        var dataXml = Directory.Exists(assembliesDir)
            ? Directory.GetFiles(assembliesDir, $"{assemblyName}.dll.data.xml", SearchOption.AllDirectories).FirstOrDefault()
            : null;
        return dataXml ?? throw new InvalidOperationException(
            $"Plugin assembly XML not found for '{assemblyName}' under PluginAssemblies/. Ensure the solution has been built or pp-plugin-assembly has been run.");
    }

    private static void ApplyPublicKeyToken(string stepFilePath, string assemblyDataXmlPath)
    {
        var assemblyDoc = new XmlDocument();
        assemblyDoc.Load(assemblyDataXmlPath);
        var fullName = assemblyDoc.DocumentElement?.GetAttribute("FullName") ?? "";
        var match = Regex.Match(fullName, "PublicKeyToken=([a-zA-Z0-9]+)");
        if (!match.Success) throw new InvalidOperationException("PublicKeyToken not found in plugin file");

        var content = File.ReadAllText(stepFilePath);
        if (content.Contains("PublicKeyToken=__public-key-token__"))
        {
            content = content.Replace("__public-key-token__", match.Groups[1].Value);
            // Writes UTF-8 without a BOM and with a trailing newline.
            File.WriteAllText(stepFilePath, content + "\r\n", new UTF8Encoding(false));
        }
    }

    private static void ApplyPluginTypeId(string stepFilePath, string assemblyDataXmlPath, string pluginClassName)
    {
        var assemblyDoc = new XmlDocument();
        assemblyDoc.Load(assemblyDataXmlPath);
        var pluginType = assemblyDoc.SelectSingleNode($"//PluginType[contains(@AssemblyQualifiedName, '{pluginClassName}')]") as XmlElement
            ?? throw new InvalidOperationException(
                $"Plugin class '{pluginClassName}' not found in assembly XML at '{assemblyDataXmlPath}'. Check the PluginName parameter matches a class in the compiled assembly.");

        var stepDoc = new XmlDocument();
        stepDoc.Load(stepFilePath);
        stepDoc.SelectSingleNode("/SdkMessageProcessingStep/PluginTypeId")!.InnerText = pluginType.GetAttribute("PluginTypeId");
        stepDoc.Save(stepFilePath);
    }
}
