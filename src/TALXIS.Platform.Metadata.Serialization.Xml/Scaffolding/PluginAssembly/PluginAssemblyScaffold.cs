using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Resolves the built plugin DLL through MSBuild, inspects its IPlugin types,
/// writes PluginAssemblies/&lt;name&gt;.dll.data.xml, and registers
/// the type-91 RootComponent.
/// </summary>
public static class PluginAssemblyScaffold
{
    private const string DefaultTargetFramework = "net472";

    public static ScaffoldResult Apply(PluginAssemblyScaffoldRequest request)
    {
        var result = new ScaffoldResult();

        var pluginRoot = Path.GetFullPath(request.PluginProjectRootPath);
        var csprojPath = Directory.GetFiles(pluginRoot, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException("csproj not found");
        var projectDirectory = Path.GetDirectoryName(csprojPath)!;
        var csprojFileName = Path.GetFileNameWithoutExtension(csprojPath);

        var csprojDoc = new XmlDocument();
        csprojDoc.Load(csprojPath);
        var assemblyName = csprojDoc.SelectNodes("//Project/PropertyGroup/AssemblyName")!.Cast<XmlNode>().LastOrDefault()?.InnerText ?? csprojFileName;
        var fileVersion = csprojDoc.SelectNodes("//Project/PropertyGroup/FileVersion")!.Cast<XmlNode>().LastOrDefault()?.InnerText ?? "1.0.0.0";

        ResolveOutputDirs(csprojPath, projectDirectory, csprojDoc, result, out var targetFramework, out var buildDir, out var publishDir);

        var sdkPath = ResolveExistingPath(buildDir, publishDir, "Microsoft.Xrm.Sdk.dll", "Microsoft.Xrm.Sdk.dll", projectDirectory, targetFramework);
        var dllPath = ResolveExistingPath(buildDir, publishDir, $"{assemblyName}.dll", "plugin assembly", pluginRoot, targetFramework);

        var (publicKeyToken, classList) = PluginAssemblyInspector.Inspect(dllPath, sdkPath);

        var fullName = $"{assemblyName}, Version={fileVersion}, Culture=neutral, PublicKeyToken={publicKeyToken}";
        var dataXmlPath = Path.Combine(request.SolutionRootPath, "PluginAssemblies", $"{assemblyName}.dll.data.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(dataXmlPath)!);
        BuildDataXml(assemblyName, fileVersion, publicKeyToken, request.AssemblyId, csprojFileName, classList).Save(dataXmlPath);

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.PluginAssembly,
            Id = Guid.Parse(request.AssemblyId),
            SchemaName = fullName,
            Behavior = 0,
        });

        return result;
    }

    private static void ResolveOutputDirs(string csprojPath, string projectDirectory, XmlDocument csprojDoc,
        ScaffoldResult result, out string targetFramework, out string buildDir, out string publishDir)
    {
        try
        {
            var props = MsBuildPropertyQuery.Query(csprojPath, "TargetFramework", "TargetDir", "PublishDir");
            targetFramework = props.TryGetValue("TargetFramework", out var tfm) ? tfm : "";
            buildDir = MsBuildPropertyQuery.ResolvePath(props.TryGetValue("TargetDir", out var td) ? td : null, projectDirectory) ?? "";
            if (string.IsNullOrWhiteSpace(targetFramework) || string.IsNullOrWhiteSpace(buildDir))
                throw new InvalidOperationException($"MSBuild did not report a usable TargetFramework/TargetDir for '{csprojPath}'.");
            publishDir = MsBuildPropertyQuery.ResolvePath(props.TryGetValue("PublishDir", out var pd) ? pd : null, projectDirectory)
                ?? Path.Combine(buildDir, "publish");
        }
        catch (Exception ex)
        {
            result.AddWarning($"Could not resolve TargetFramework/output paths via MSBuild for '{csprojPath}' ({ex.Message}); falling back to reading the csproj directly with a '{DefaultTargetFramework}' default.");
            targetFramework = ResolveFallbackTargetFramework(csprojDoc);
            buildDir = Path.Combine(projectDirectory, "bin", "Debug", targetFramework);
            publishDir = Path.Combine(buildDir, "publish");
        }
    }

    private static string ResolveFallbackTargetFramework(XmlDocument csprojDoc)
    {
        var tfm = csprojDoc.SelectNodes("//Project/PropertyGroup/TargetFramework")!.Cast<XmlNode>().LastOrDefault()?.InnerText;
        if (string.IsNullOrWhiteSpace(tfm))
        {
            var tfms = csprojDoc.SelectNodes("//Project/PropertyGroup/TargetFrameworks")!.Cast<XmlNode>().LastOrDefault()?.InnerText;
            tfm = tfms?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).FirstOrDefault();
        }
        return string.IsNullOrWhiteSpace(tfm) ? DefaultTargetFramework : tfm!;
    }

    // Prefer the published (ILRepack-merged) output when it exists; only a build has
    // necessarily run by scaffold time.
    private static string ResolveExistingPath(string buildDir, string publishDir, string fileName, string description, string projectDirectory, string targetFramework)
    {
        var publishPath = Path.Combine(publishDir, fileName);
        var buildPath = Path.Combine(buildDir, fileName);
        if (File.Exists(publishPath)) return publishPath;
        if (File.Exists(buildPath)) return buildPath;
        throw new FileNotFoundException(
            $"Could not find {description} ('{fileName}'). Probed:\n  {buildPath}\n  {publishPath}\n" +
            $"Ensure the plugin project at '{projectDirectory}' has been built (dotnet build/publish) for TargetFramework '{targetFramework}'.",
            buildPath);
    }

    internal static XmlDocument BuildDataXml(string assemblyName, string fileVersion, string publicKeyToken,
        string assemblyId, string csprojFileName, IReadOnlyList<string> classList)
    {
        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));

        var root = doc.CreateElement("PluginAssembly");
        root.SetAttribute("FullName", $"{assemblyName}, Version={fileVersion}, Culture=neutral, PublicKeyToken={publicKeyToken}");
        root.SetAttribute("PluginAssemblyId", assemblyId);
        root.SetAttribute("CustomizationLevel", "1");
        root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
        doc.AppendChild(root);

        var isolationMode = doc.CreateElement("IsolationMode");
        isolationMode.InnerText = "2";
        root.AppendChild(isolationMode);

        var sourceType = doc.CreateElement("SourceType");
        sourceType.InnerText = "0";
        root.AppendChild(sourceType);

        var fileName = doc.CreateElement("FileName");
        fileName.InnerText = $"/PluginAssemblies/{assemblyName}.dll";
        root.AppendChild(fileName);

        var pluginTypes = doc.CreateElement("PluginTypes");
        root.AppendChild(pluginTypes);

        foreach (var className in classList)
        {
            if (className == $"{csprojFileName}.PluginBase") continue;

            var pluginType = doc.CreateElement("PluginType");
            pluginType.SetAttribute("AssemblyQualifiedName", $"{className}, {assemblyName}, Version={fileVersion}, Culture=neutral, PublicKeyToken={publicKeyToken}");
            pluginType.SetAttribute("PluginTypeId", Guid.NewGuid().ToString("D"));
            pluginType.SetAttribute("Name", className);

            var friendlyName = doc.CreateElement("FriendlyName");
            friendlyName.InnerText = Guid.NewGuid().ToString("D");
            pluginType.AppendChild(friendlyName);

            pluginTypes.AppendChild(pluginType);
        }

        return doc;
    }
}
