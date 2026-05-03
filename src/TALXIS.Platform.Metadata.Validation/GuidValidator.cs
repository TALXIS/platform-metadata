using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Scans XML files in a workspace directory for duplicate GUIDs across component types.
/// </summary>
public sealed class GuidValidator
{
    private static readonly Regex GuidPattern = new Regex(
        @"\{?[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}\}?",
        RegexOptions.Compiled);

    /// <summary>
    /// Identity rules: which element names carry a component GUID, and which
    /// file-path substring(s) the rule applies to. An element is only considered
    /// an identity element when its local name matches AND the file path contains
    /// at least one of the associated patterns.
    /// </summary>
    private static readonly (string ElementName, string FilePattern)[] IdentityRules =
    {
        ("savedqueryid",                    "SavedQueries"),
        ("savedqueryvisualizationid",       "SavedQueries"),
        ("formid",                          "FormXml"),
        ("WebResourceId",                   ".data.xml"),
        ("WorkflowId",                      "Workflows"),
        ("SdkMessageProcessingStepId",      "SdkMessageProcessingSteps"),
        ("PluginTypeId",                    "PluginAssemblies"),
        ("connectionroleid",                "ConnectionRoles"),
        ("OptionSetId",                     "OptionSets"),
        ("environmentvariabledefinitionid", "environmentvariabledefinitions"),
        ("environmentvariablevalueid",      "environmentvariabledefinitions"),
        ("AppModuleId",                     "AppModules"),
        ("RoleId",                          "Roles"),
    };

    private static readonly Dictionary<string, List<string>> IdentityLookup;

    static GuidValidator()
    {
        IdentityLookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (elementName, filePattern) in IdentityRules)
        {
            if (!IdentityLookup.TryGetValue(elementName, out var patterns))
            {
                patterns = new List<string>();
                IdentityLookup[elementName] = patterns;
            }
            patterns.Add(filePattern);
        }
    }

    private struct GuidLocation
    {
        public string FilePath;
        public string ElementName;
        public int Line;
        public int Column;
    }

    /// <summary>
    /// Scans all XML files under <paramref name="workspacePath"/> for duplicate
    /// component GUIDs and returns one <see cref="ValidationResult"/> per occurrence.
    /// </summary>
    public IReadOnlyList<ValidationResult> ValidateDirectory(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
        {
            return new[]
            {
                new ValidationResult(ValidationSeverity.Error,
                    $"Directory not found: {workspacePath}", null, null, null)
            };
        }

        var guidMap = new Dictionary<string, List<GuidLocation>>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ValidationResult>();

        foreach (var filePath in Directory.EnumerateFiles(workspacePath, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                ScanFile(filePath, guidMap);
            }
            catch (System.Xml.XmlException ex)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning, $"Malformed XML, skipped GUID scan: {ex.Message}", filePath, null, null));
            }
            catch (IOException ex)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning, $"Cannot read file, skipped GUID scan: {ex.Message}", filePath, null, null));
            }
        }

        foreach (var entry in guidMap)
        {
            var locations = entry.Value;
            if (locations.Count < 2)
                continue;

            var guid = entry.Key;

            for (int i = 0; i < locations.Count; i++)
            {
                var loc = locations[i];
                var otherFiles = locations
                    .Where((_, idx) => idx != i)
                    .Select(l => Path.GetFileName(l.FilePath))
                    .ToArray();

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    $"Duplicate GUID {{{guid}}} in <{loc.ElementName}>. Also found in: {string.Join(", ", otherFiles)}",
                    loc.FilePath,
                    loc.Line > 0 ? loc.Line : null,
                    loc.Column > 0 ? loc.Column : null));
            }
        }

        return results;
    }

    private static void ScanFile(string filePath, Dictionary<string, List<GuidLocation>> guidMap)
    {
        var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo);
        if (doc.Root != null)
            ScanElements(doc.Root, filePath, guidMap);
    }

    private static void ScanElements(XElement element, string filePath, Dictionary<string, List<GuidLocation>> guidMap)
    {
        if (!element.HasElements)
        {
            var localName = element.Name.LocalName;

            if (!IsInsideParameters(element) && IsIdentityElement(localName, filePath))
            {
                var value = element.Value.Trim();
                if (GuidPattern.IsMatch(value))
                {
                    var normalized = NormalizeGuid(value);
                    if (normalized != null)
                    {
                        var lineInfo = (IXmlLineInfo)element;
                        AddGuid(guidMap, normalized, new GuidLocation
                        {
                            FilePath = filePath,
                            ElementName = localName,
                            Line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
                            Column = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0
                        });
                    }
                }
            }
        }

        foreach (var child in element.Elements())
        {
            ScanElements(child, filePath, guidMap);
        }
    }

    private static bool IsInsideParameters(XElement element)
    {
        var parent = element.Parent;
        while (parent != null)
        {
            if (parent.Name.LocalName.Equals("parameters", StringComparison.OrdinalIgnoreCase))
                return true;
            parent = parent.Parent;
        }
        return false;
    }

    private static bool IsIdentityElement(string elementName, string filePath)
    {
        if (!IdentityLookup.TryGetValue(elementName, out var patterns))
            return false;

        if (patterns.Count == 0)
            return true;

        foreach (var pattern in patterns)
        {
            if (filePath.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string? NormalizeGuid(string raw)
    {
        var stripped = raw.Trim().TrimStart('{').TrimEnd('}').ToLowerInvariant();
        if (Guid.TryParse(stripped, out var parsed))
            return parsed.ToString("D");
        return null;
    }

    private static void AddGuid(Dictionary<string, List<GuidLocation>> map, string guid, GuidLocation location)
    {
        if (!map.TryGetValue(guid, out var list))
        {
            list = new List<GuidLocation>();
            map[guid] = list;
        }
        list.Add(location);
    }
}
