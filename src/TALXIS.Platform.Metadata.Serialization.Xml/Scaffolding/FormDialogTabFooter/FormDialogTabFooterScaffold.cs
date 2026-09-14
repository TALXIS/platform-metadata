namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-dialog-tabfooter template post-action
/// scripts. Appends an empty tabfooter element to the resolved tab of a dialog form.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormDialogTabFooterScaffold
{
    public static ScaffoldResult Apply(FormDialogTabFooterScaffoldRequest request)
    {
        var formFilePath = LocateDialog(request);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var tab = FormPlacementResolver.ResolveTab(formDoc, request.Placement);

        var footer = formDoc.CreateElement("tabfooter");
        footer.SetAttribute("id", "{" + (request.TabFooterId ?? Guid.NewGuid().ToString()) + "}");
        tab.AppendChild(footer);

        // The old script saved with plain XmlDocument.Save, not the shared writer settings.
        formDoc.Save(formFilePath);
        return new ScaffoldResult();
    }

    // The old script's newest-dialog fallback was dead code (brace mismatch in its
    // sentinel check made it unreachable); this implements the intended behavior.
    private static string LocateDialog(FormDialogTabFooterScaffoldRequest request)
    {
        var dialogsRoot = Path.Combine(request.SolutionRootPath, "Dialogs");
        if (request.FormId != null)
        {
            var path = Path.Combine(dialogsRoot, "{" + request.FormId + "}.xml");
            if (!File.Exists(path)) throw new FileNotFoundException($"Form file not found: {path}");
            return path;
        }

        var latest = Directory.Exists(dialogsRoot)
            ? new DirectoryInfo(dialogsRoot).EnumerateFiles("*.xml").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
            : null;
        return latest?.FullName
            ?? throw new FileNotFoundException($"No XML forms found in directory: {dialogsRoot}");
    }
}
