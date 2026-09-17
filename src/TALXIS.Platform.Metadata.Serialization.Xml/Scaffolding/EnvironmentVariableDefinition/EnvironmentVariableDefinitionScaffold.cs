using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-environment-variable-definition template
/// post-action scripts: drops the empty defaultvalue/description leftovers of
/// blank parameters, rewrites the definition with the old Finalize.ps1 writer
/// settings and registers it in Solution.xml (type 380, by schema name).
/// Like the old script, registration is skipped with a warning when the project
/// carries no Other/Solution.xml.
/// </summary>
public static class EnvironmentVariableDefinitionScaffold
{
    public static ScaffoldResult Apply(EnvironmentVariableDefinitionScaffoldRequest request)
    {
        var result = new ScaffoldResult();
        var doc = new XmlDocument();
        doc.Load(request.DefinitionFilePath);
        var root = doc.DocumentElement!;

        // A blank DefaultValue/Description parameter leaves an empty element behind,
        // which is semantically different from "no default" - remove it.
        if (root.SelectSingleNode("defaultvalue") is XmlElement defaultValue && string.IsNullOrEmpty(defaultValue.InnerText))
            root.RemoveChild(defaultValue);
        if (root.SelectSingleNode("description") is XmlElement description && string.IsNullOrEmpty(description.GetAttribute("default")))
            root.RemoveChild(description);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = true,
            Encoding = new System.Text.UTF8Encoding(false),
        };
        using (var writer = XmlWriter.Create(request.DefinitionFilePath, settings))
        {
            doc.Save(writer);
        }

        if (!File.Exists(Path.Combine(request.SolutionRootPath, "Other", "Solution.xml")))
        {
            result.AddWarning($"Solution.xml not found under '{request.SolutionRootPath}' - skipping RootComponent registration.");
            return result;
        }

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.EnvironmentVariableDefinition,
            SchemaName = request.SchemaName,
            Behavior = 0,
        });
        return result;
    }
}
