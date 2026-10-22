using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Packaging;

// SolutionPackagerLib matches root components only against top-level customizations nodes, so an entity
// form (nested under Entities/Entity/FormXml) is always reported as missing even when it was packed.
internal static class PackedFormRootComponentFilter
{
    private const string SystemFormType = "SystemForm";

    // One row of the packager's PrintComponentInfos output. The key may contain quotes (schema names are not validated).
    private static readonly Regex ComponentPattern =
        new(@"^Type='(?<type>[^']+)', Id \(or schema name\)='(?<key>.*)'\.$", RegexOptions.Compiled);

    public static SolutionPackagerResult Apply(SolutionPackagerResult result, string zipPath)
    {
        if (!result.HasMissingRootComponents || !File.Exists(zipPath)) return result;

        var packedFormIds = ReadPackedFormIds(zipPath);
        if (packedFormIds.Count == 0) return result;

        // Rewrite the warning in Warnings itself, so MissingRootComponentWarnings is still derived from (and a subset of) it.
        var warnings = result.Warnings
            .Select(warning => warning.StartsWith(SolutionPackagerResult.MissingRootComponentsWarningPrefix, StringComparison.OrdinalIgnoreCase)
                ? RemovePackedForms(warning, packedFormIds)
                : warning)
            .OfType<string>()
            .ToArray();

        return new SolutionPackagerResult(result.Errors, warnings);
    }

    private static string? RemovePackedForms(string warning, IReadOnlySet<string> packedFormIds)
    {
        // The first line is the header. If any row is not in the expected format, keep the warning as is rather than lose it.
        var components = warning.Split('\n')
            .Skip(1)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => ComponentPattern.Match(line))
            .ToArray();
        if (components.Length == 0 || components.Any(component => !component.Success)) return warning;

        var stillMissing = components
            .Where(component => !IsPackedForm(component, packedFormIds))
            .Select(component => "  " + component.Value)
            .ToArray();

        if (stillMissing.Length == components.Length) return warning;
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
