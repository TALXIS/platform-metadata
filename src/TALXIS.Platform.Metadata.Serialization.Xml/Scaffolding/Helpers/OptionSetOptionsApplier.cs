using System.Text.RegularExpressions;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Parses option specifications and applies them to a local attribute file
/// or a global option set.
/// </summary>
internal static class OptionSetOptionsApplier
{
    private const int AutoOptionValueStart = 100000000;

    // Label:Value pairs pin explicit values; bare labels auto-increment from 100000000.
    public static List<OptionMetadata> ParseOptions(string spec)
    {
        var options = new List<OptionMetadata>();
        var nextAutoValue = AutoOptionValueStart;
        foreach (var entry in ParseOptionEntries(spec))
        {
            var match = Regex.Match(entry, @"^(.+):(\d+)$");
            var label = match.Success ? match.Groups[1].Value.Trim() : entry;
            var value = match.Success ? int.Parse(match.Groups[2].Value) : nextAutoValue++;
            options.Add(new OptionMetadata
            {
                Value = value,
                Label = new Label(label),
                Description = new Label(""),
            });
        }
        return options;
    }

    // Global sets go through the workspace model: the reader/writer own the option set
    // file shape and the RootComponent (type 9) registration in Solution.xml.
    public static void ApplyToGlobalOptionSet(string solutionRootPath, string optionSetName, string? rootComponentSchemaName, List<OptionMetadata> options)
    {
        var workspace = new XmlWorkspaceReader().Load(solutionRootPath);

        var optionSet = workspace.GlobalOptionSets.FirstOrDefault(o => string.Equals(o.Name, optionSetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Global option set '{optionSetName}' not found in '{solutionRootPath}'.");
        foreach (var option in options)
        {
            optionSet.AddOption(option);
        }

        if (rootComponentSchemaName != null)
        {
            var solution = workspace.Solutions.FirstOrDefault()
                ?? throw new InvalidOperationException($"No solution manifest found in '{solutionRootPath}'.");
            var exists = solution.RootComponents.Any(rc =>
                rc.Type == ComponentType.OptionSet &&
                string.Equals(rc.SchemaName, rootComponentSchemaName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                solution.AddRootComponent(new RootComponent
                {
                    Type = ComponentType.OptionSet,
                    SchemaName = rootComponentSchemaName,
                    Behavior = 0,
                });
            }
        }

        new XmlWorkspaceWriter().Write(workspace, solutionRootPath);
    }

    // Local sets stay file-level (the rendered attribute is not part of the workspace),
    // but the option XML shape is borrowed from the writer.
    public static void ApplyToLocalAttribute(string attributeFilePath, List<OptionMetadata> options)
    {
        var doc = XDocument.Load(attributeFilePath);
        var optionsElement = doc.Descendants("options").FirstOrDefault()
            ?? throw new InvalidOperationException($"Options node not found in '{attributeFilePath}'.");
        foreach (var option in options)
        {
            optionsElement.Add(XmlWorkspaceWriter.BuildOptionElement(option));
        }
        ScaffoldXmlFile.Save(doc, attributeFilePath);
    }

    private static IEnumerable<string> ParseOptionEntries(string spec) =>
        spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Replace("{", "").Replace("}", "").Trim())
            .Where(e => e.Length > 0);
}
