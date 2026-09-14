using System.Text.RegularExpressions;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-section template post-action scripts.
/// Finalizes the rendered section fragment (generated section id, normalized name
/// attribute - the old SetVariables.ps1) and appends it into the sections container
/// of the resolved column, creating the container when the column has none.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormSectionScaffold
{
    public static ScaffoldResult Apply(FormSectionScaffoldRequest request)
    {
        if (!File.Exists(request.SectionFilePath))
            throw new FileNotFoundException($"Section fragment file not found: {request.SectionFilePath}");

        var fragment = File.ReadAllText(request.SectionFilePath);
        var sectionId = request.SectionId ?? Guid.NewGuid().ToString();
        // Mirrors SetVariables.ps1: the rendered fragment carries the raw parameter
        // value ("unknown" when omitted), which is swapped for the final id here.
        fragment = fragment.Replace(request.SectionId ?? "unknown", sectionId);
        var processedName = Regex.Replace(request.SectionName.ToLower(), "[^a-z0-9]", "");
        fragment = fragment.Replace("examplesectionname", processedName);

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var column = FormPlacementResolver.ResolveTargetColumn(formDoc, request.Placement);

        var sectionsNode = column.SelectSingleNode("./sections");
        if (sectionsNode == null)
        {
            sectionsNode = formDoc.CreateElement("sections");
            column.AppendChild(sectionsNode);
        }

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<sections>" + fragment + "</sections>");
        foreach (XmlNode section in fragmentDoc.DocumentElement!.ChildNodes)
        {
            sectionsNode.AppendChild(formDoc.ImportNode(section, deep: true));
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }
}
