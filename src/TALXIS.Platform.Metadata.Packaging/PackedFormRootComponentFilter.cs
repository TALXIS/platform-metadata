using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Packaging;

// SolutionPackagerLib matches root components only against top-level customizations nodes, so an entity
// form (nested under Entities/Entity/FormXml) is always reported as missing even when it was packed.
internal static class PackedFormRootComponentFilter
{
    private const string SystemFormType = "SystemForm";

    private static readonly Regex ComponentPattern =
        new(@"Type='(?<type>[^']+)',\s*Id \(or schema name\)='(?<key>[^']+)'\.", RegexOptions.Compiled);

    public static SolutionPackagerResult Apply(SolutionPackagerResult result, string zipPath)
    {
        if (!result.HasMissingRootComponents || !File.Exists(zipPath)) return result;

        var packedFormIds = ReadPackedFormIds(zipPath);
        if (packedFormIds.Count == 0) return result;

        var realMissingWarnings = result.MissingRootComponentWarnings
            .Select(warning => RemovePackedForms(warning, packedFormIds))
            .OfType<string>()
            .ToArray();

        return new SolutionPackagerResult(result.Errors, result.Warnings, realMissingWarnings);
    }

    private static string? RemovePackedForms(string warning, IReadOnlySet<string> packedFormIds)
    {
        var components = ComponentPattern.Matches(warning);
        if (components.Count == 0) return warning;

        var stillMissing = components
            .Where(component => !IsPackedForm(component, packedFormIds))
            .Select(component => "  " + component.Value)
            .ToArray();

        if (stillMissing.Length == components.Count) return warning;
        if (stillMissing.Length == 0) return null;

        return SolutionPackagerResult.MissingRootComponentsWarningPrefix + ":" + Environment.NewLine
            + string.Join(Environment.NewLine, stillMissing);
    }

    private static bool IsPackedForm(Match component, IReadOnlySet<string> packedFormIds)
    {
        return component.Groups["type"].Value == SystemFormType
            && packedFormIds.Contains(NormalizeId(component.Groups["key"].Value));
    }

    private static IReadOnlySet<string> ReadPackedFormIds(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var customizations = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase));
        if (customizations == null) return new HashSet<string>();

        using var stream = customizations.Open();
        return XDocument.Load(stream)
            .Descendants("systemform")
            .Select(form => NormalizeId((string?)form.Element("formid")))
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeId(string? value)
    {
        return Guid.TryParse(value, out var id) ? id.ToString("b") : value?.Trim() ?? string.Empty;
    }
}
