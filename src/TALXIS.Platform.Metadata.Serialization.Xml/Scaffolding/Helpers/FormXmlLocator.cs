using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Finds the form XML file a form template targets, mirroring the shared
/// LocateForm.ps1 fallback chain: exact path, per-name search, newest form.
/// </summary>
internal static class FormXmlLocator
{
    public static string Locate(string solutionRootPath, string? entitySchemaName, string? formType, string? formId)
    {
        var entitiesRoot = Path.Combine(solutionRootPath, SolutionPackagerLayout.EntitiesDirectory);
        var dialogsRoot = Path.Combine(solutionRootPath, "Dialogs");

        // Nothing known: newest form file across all entities and dialogs.
        if (formType == null && entitySchemaName == null && formId == null)
        {
            var files = EntityFormFiles(entitiesRoot).Concat(XmlFiles(dialogsRoot)).ToList();
            if (files.Count == 0)
                throw new FileNotFoundException($"No form XML files found under '{entitiesRoot}' in FormXml\\main or FormXml\\quickCreate.");
            return files
                .OrderByDescending(f => f.LastWriteTime > f.CreationTime ? f.LastWriteTime : f.CreationTime)
                .First().FullName;
        }

        // Form type or entity unknown: search by file name, dialog files appended as fallback.
        if (formType == null || entitySchemaName == null)
        {
            var targetName = "{" + (formId ?? "unknownFormId") + "}.xml";
            var matches = EntityFormFiles(entitiesRoot)
                .Where(f => string.Equals(f.Name, targetName, StringComparison.OrdinalIgnoreCase))
                .Concat(XmlFiles(dialogsRoot))
                .ToList();
            if (matches.Count == 0)
                throw new FileNotFoundException($"File '{targetName}' not found under '{entitiesRoot}' in FormXml\\main or FormXml\\quickCreate.");
            return matches[0].FullName;
        }

        // Form id unknown: newest form in the known entity/type directory (plus dialogs).
        if (formId == null)
        {
            var formDirectory = Path.Combine(entitiesRoot, entitySchemaName, "FormXml", formType);
            var files = XmlFiles(formDirectory).Concat(XmlFiles(dialogsRoot)).ToList();
            if (files.Count == 0) throw new FileNotFoundException($"No XML forms found in directory: {formDirectory}");
            return files.OrderByDescending(f => f.LastWriteTime).First().FullName;
        }

        // Case-insensitive like the PowerShell -eq the scripts used (pp-form-event-handler passes "Dialog").
        var path = string.Equals(formType, "dialog", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(dialogsRoot, "{" + formId + "}.xml")
            : Path.Combine(entitiesRoot, entitySchemaName, "FormXml", formType, "{" + formId + "}.xml");
        if (!File.Exists(path)) throw new FileNotFoundException($"Form file not found: {path}");
        return path;
    }

    private static IEnumerable<FileInfo> EntityFormFiles(string entitiesRoot)
    {
        if (!Directory.Exists(entitiesRoot)) yield break;
        foreach (var directoryPath in Directory.EnumerateDirectories(entitiesRoot, "*", SearchOption.AllDirectories))
        {
            var directory = new DirectoryInfo(directoryPath);
            if (directory.Parent?.Name != "FormXml") continue;
            if (directory.Name != "main" && directory.Name != "quickCreate") continue;
            foreach (var file in directory.EnumerateFiles("*.xml"))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<FileInfo> XmlFiles(string directory) =>
        Directory.Exists(directory) ? new DirectoryInfo(directory).EnumerateFiles("*.xml") : Enumerable.Empty<FileInfo>();
}
