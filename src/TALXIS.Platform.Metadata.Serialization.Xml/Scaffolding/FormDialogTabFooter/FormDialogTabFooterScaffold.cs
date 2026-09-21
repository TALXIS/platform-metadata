namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends an empty tabfooter element to the resolved tab of a dialog form.
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

        // Saves the document using XmlDocument.Save with its default settings.
        formDoc.Save(formFilePath);
        return new ScaffoldResult();
    }

    // Selects the newest dialog when no form id is supplied.
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
