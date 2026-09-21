using System.Text.RegularExpressions;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Finalizes the rendered section fragment with a generated section id and a normalized
/// name attribute, then appends its sections to the resolved column. Creates the
/// sections container when the column has none.
/// </summary>
public static class FormSectionScaffold
{
    public static ScaffoldResult Apply(FormSectionScaffoldRequest request)
    {
        if (!File.Exists(request.SectionFilePath))
            throw new FileNotFoundException($"Section fragment file not found: {request.SectionFilePath}");

        var fragment = File.ReadAllText(request.SectionFilePath);
        var sectionId = request.SectionId ?? Guid.NewGuid().ToString();
        // Replaces the raw id parameter in the rendered fragment ("unknown" when omitted)
        // with the final section id.
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
