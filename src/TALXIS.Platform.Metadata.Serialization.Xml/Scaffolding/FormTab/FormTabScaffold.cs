using System.Text.RegularExpressions;
using System.Xml;
using TALXIS.Platform.Metadata.Layout;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-tab template post-action scripts.
/// Optionally removes the scaffolded default tab, finalizes the rendered tab
/// fragment (generated tab id, normalized name - the old SetVariables.ps1) and
/// appends it into the form's tabs container.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormTabScaffold
{
    public static ScaffoldResult Apply(FormTabScaffoldRequest request)
    {
        if (!File.Exists(request.TabFilePath))
            throw new FileNotFoundException($"Tab fragment file not found: {request.TabFilePath}");

        var result = new ScaffoldResult();
        if (request.RemoveDefaultTab) RemoveDefaultTab(request, result);

        var fragment = File.ReadAllText(request.TabFilePath);
        var tabId = request.TabId ?? Guid.NewGuid().ToString();
        // Mirrors SetVariables.ps1: the rendered fragment carries the raw parameter
        // value ("unknownTabId" when omitted), which is swapped for the final id here.
        fragment = fragment.Replace(request.TabId ?? "unknownTabId", tabId);
        var processedName = Regex.Replace(request.DisplayName.ToLower(), "[^a-z0-9]", "");
        fragment = fragment.Replace("exampletabname", processedName);

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var tabsNode = formDoc.SelectSingleNode("//tabs")
            ?? throw new InvalidOperationException($"Tabs node not found in '{formFilePath}'.");

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<tabs>" + fragment + "</tabs>");
        foreach (XmlNode tab in fragmentDoc.DocumentElement!.ChildNodes)
        {
            tabsNode.AppendChild(formDoc.ImportNode(tab, deep: true));
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return result;
    }

    // The old RemoveDefaultTab.ps1 located the form on its own from the raw parameter
    // values (no dialog support), so unknown entity/form-type reproduce its failure mode.
    private static void RemoveDefaultTab(FormTabScaffoldRequest request, ScaffoldResult result)
    {
        var formDirectory = Path.Combine(
            request.SolutionRootPath, SolutionPackagerLayout.EntitiesDirectory,
            request.EntitySchemaName ?? "unknown", "FormXml", request.FormType ?? "unknown");

        string formFilePath;
        if (request.FormId == null)
        {
            var latest = Directory.Exists(formDirectory)
                ? new DirectoryInfo(formDirectory).EnumerateFiles("*.xml").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
                : null;
            formFilePath = latest?.FullName
                ?? throw new FileNotFoundException($"No XML forms found in directory: {formDirectory}");
        }
        else
        {
            formFilePath = Path.Combine(formDirectory, "{" + request.FormId + "}.xml");
            if (!File.Exists(formFilePath)) throw new FileNotFoundException($"Form file not found: {formFilePath}");
        }

        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var generalTab = formDoc.SelectSingleNode("//tab[@name=\"generaltab\"]");
        if (generalTab == null)
        {
            result.AddWarning($"Tab with name='generaltab' not found in {formFilePath}");
            return;
        }

        generalTab.ParentNode!.RemoveChild(generalTab);
        ScaffoldXmlFile.Save(formDoc, formFilePath);
    }
}
