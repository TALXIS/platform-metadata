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

    // Filename suffix of a component's managed-layer counterpart ({id}_managed.xml).
    private const string ManagedLayerSuffix = "_managed";

    // Never scanned for GUIDds
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".github", "node_modules", "packages", "TestResults"
    };

    /// <summary>
    /// Identity rules for child elements: the element's text content carries
    /// a component GUID when its local name matches AND the file path contains
    /// at least one of the associated patterns.
    /// </summary>
    private static readonly (string ElementName, string FilePattern)[] IdentityRules =
    {
        ("savedqueryid",                    "SavedQueries"),
        ("savedqueryvisualizationid",       "SavedQueries"),
        ("formid",                          "FormXml"),
        ("WebResourceId",                   ".data.xml"),
        ("WorkflowId",                      "Workflows"),
        ("PluginTypeId",                    "PluginAssemblies"),
        ("connectionroleid",                "ConnectionRoles"),
        ("OptionSetId",                     "OptionSets"),
        ("environmentvariabledefinitionid", "environmentvariabledefinitions"),
        ("environmentvariablevalueid",      "environmentvariabledefinitions"),
    };

    /// <summary>
    /// Identity rules for attributes on root/top-level elements: the attribute
    /// value carries a component GUID when the attribute name matches AND the
    /// file path contains at least one of the associated patterns.
    /// </summary>
    private static readonly (string AttributeName, string FilePattern)[] AttributeIdentityRules =
    {
        ("SdkMessageProcessingStepId",  "SdkMessageProcessingSteps"),
        ("PluginAssemblyId",            "PluginAssemblies"),
        ("WorkflowId",                  "Workflows"),
        ("id",                          "Roles"),
        ("id",                          "ConnectionRoles"),
        ("AppModuleId",                 "AppModules"),
    };

    private static readonly Dictionary<string, List<string>> IdentityLookup;
    private static readonly Dictionary<string, List<string>> AttributeIdentityLookup;

    static GuidValidator()
    {
        IdentityLookup = BuildLookup(IdentityRules);
        AttributeIdentityLookup = BuildLookup(AttributeIdentityRules);
    }

    private static Dictionary<string, List<string>> BuildLookup((string Name, string FilePattern)[] rules)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, filePattern) in rules)
        {
            if (!lookup.TryGetValue(name, out var patterns))
            {
                patterns = new List<string>();
                lookup[name] = patterns;
            }
            patterns.Add(filePattern);
        }
        return lookup;
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
            var relativePath = filePath.Substring(workspacePath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => IgnoredDirectories.Contains(s)))
                continue;

            try
            {
                ScanFile(filePath, relativePath, guidMap);
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
                var locComponent = GetComponentKey(loc.FilePath);

                var otherFiles = locations
                    .Where((l, idx) => idx != i &&
                        !string.Equals(GetComponentKey(l.FilePath), locComponent, StringComparison.OrdinalIgnoreCase))
                    .Select(l => Path.GetFileName(l.FilePath))
                    .ToArray();

                if (otherFiles.Length == 0)
                    continue;

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

    private static void ScanFile(string filePath, string relativePath, Dictionary<string, List<GuidLocation>> guidMap)
    {
        var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo);
        if (doc.Root != null)
            ScanElements(doc.Root, filePath, relativePath, guidMap);
    }

    private static void ScanElements(XElement element, string filePath, string relativePath, Dictionary<string, List<GuidLocation>> guidMap)
    {
        // Scan attributes for identity GUIDs
        foreach (var attr in element.Attributes())
        {
            if (IsIdentityAttribute(attr.Name.LocalName, relativePath))
            {
                var value = attr.Value.Trim();
                if (GuidPattern.IsMatch(value))
                {
                    var normalized = NormalizeGuid(value);
                    if (normalized != null)
                    {
                        var lineInfo = (IXmlLineInfo)element;
                        AddGuid(guidMap, normalized, new GuidLocation
                        {
                            FilePath = filePath,
                            ElementName = $"@{attr.Name.LocalName}",
                            Line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
                            Column = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0
                        });
                    }
                }
            }
        }

        // Scan leaf element text for identity GUIDs
        if (!element.HasElements)
        {
            var localName = element.Name.LocalName;

            if (!IsInsideParameters(element) && IsIdentityElement(localName, relativePath))
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
            ScanElements(child, filePath, relativePath, guidMap);
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
        return MatchesLookup(IdentityLookup, elementName, filePath);
    }

    private static bool IsIdentityAttribute(string attributeName, string filePath)
    {
        return MatchesLookup(AttributeIdentityLookup, attributeName, filePath);
    }

    private static bool MatchesLookup(Dictionary<string, List<string>> lookup, string name, string filePath)
    {
        if (!lookup.TryGetValue(name, out var patterns))
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

    // Identity key = path with the managed-layer suffix stripped, so {name}.xml and
    // {name}_managed.xml map to the same component (never flagged as duplicates).
    private static string GetComponentKey(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);

        if (nameNoExt.EndsWith(ManagedLayerSuffix, StringComparison.OrdinalIgnoreCase))
            nameNoExt = nameNoExt.Substring(0, nameNoExt.Length - ManagedLayerSuffix.Length);

        return Path.Combine(directory, nameNoExt + ext);
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
