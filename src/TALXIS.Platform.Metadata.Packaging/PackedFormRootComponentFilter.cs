using System.IO.Compression;
using System.Xml;

namespace TALXIS.Platform.Metadata.Packaging;

// SolutionPackagerLib matches root components only against top-level customizations nodes, so an entity
// form (nested under Entities/Entity/FormXml) is always reported as missing even when it was packed.
internal static class PackedFormRootComponentFilter
{
    private const string SystemFormType = "SystemForm";

    // Element path of an entity form id in customizations.xml.
    private static readonly string[] FormIdPath = ["ImportExportXml", "Entities", "Entity", "FormXml", "forms", "systemform", "formid"];

    public static SolutionPackagerResult Apply(SolutionPackagerResult result, string zipPath)
    {
        if (!result.HasMissingRootComponents || !File.Exists(zipPath)) return result;

        IReadOnlySet<string> packedFormIds;
        try
        {
            packedFormIds = ReadPackedFormIds(zipPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or XmlException or UnauthorizedAccessException)
        {
            // We can't tell which forms were packed, so keep the packager's warnings as they are.
            return result;
        }

        if (packedFormIds.Count == 0) return result;

        // Rewrite the warning in Warnings itself, so MissingRootComponentWarnings is still derived from (and a subset of) it.
        var warnings = result.Warnings
            .Select(warning => SolutionPackagerResult.IsMissingRootComponentsWarning(warning)
                ? RemovePackedForms(warning, packedFormIds)
                : warning)
            .OfType<string>()
            .ToArray();

        return new SolutionPackagerResult(result.Errors, warnings);
    }

    private static string? RemovePackedForms(string warning, IReadOnlySet<string> packedFormIds)
    {
        // If any row is not in the expected format, keep the warning as is rather than lose it.
        if (!MissingRootComponent.TryParseWarning(warning, out var components)) return warning;

        var stillMissing = components
            .Where(component => !IsPackedForm(component, packedFormIds))
            .ToArray();

        if (stillMissing.Length == components.Count) return warning;
        if (stillMissing.Length == 0) return null;

        return MissingRootComponent.FormatWarning(stillMissing);
    }

    private static bool IsPackedForm(MissingRootComponent component, IReadOnlySet<string> packedFormIds)
    {
        return component.Type == SystemFormType
            && packedFormIds.Contains(NormalizeId(component.Key));
    }

    private static IReadOnlySet<string> ReadPackedFormIds(string zipPath)
    {
        var formIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(zipPath);
        var customizations = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase));
        if (customizations == null) return formIds;

        // customizations.xml can be hundreds of MB, so stream it and only descend into elements on FormIdPath.
        using var stream = customizations.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true });
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            var depth = reader.Depth;
            if (depth >= FormIdPath.Length || reader.LocalName != FormIdPath[depth])
            {
                reader.Skip();
                continue;
            }

            if (depth == FormIdPath.Length - 1)
            {
                var id = NormalizeId(reader.ReadElementContentAsString());
                if (id.Length > 0) formIds.Add(id);
                continue;
            }

            reader.Read();
        }

        return formIds;
    }

    private static string NormalizeId(string? value)
    {
        return Guid.TryParse(value, out var id) ? id.ToString("b") : value?.Trim() ?? string.Empty;
    }
}
