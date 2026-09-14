using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-row template post-action scripts.
/// Appends the rendered row(s) into the resolved section (or tab footer) of the
/// located form, creating the rows container when the section has none.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormRowScaffold
{
    public static ScaffoldResult Apply(FormRowScaffoldRequest request)
    {
        if (!File.Exists(request.RowFilePath))
            throw new FileNotFoundException($"Row fragment file not found: {request.RowFilePath}");

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var section = FormPlacementResolver.ResolveTargetSection(formDoc, request.Placement);

        var rowsNode = section.SelectSingleNode("./rows");
        if (rowsNode == null)
        {
            rowsNode = formDoc.CreateElement("rows");
            section.AppendChild(rowsNode);
        }

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<rows>" + File.ReadAllText(request.RowFilePath) + "</rows>");
        foreach (XmlNode row in fragmentDoc.DocumentElement!.ChildNodes)
        {
            rowsNode.AppendChild(formDoc.ImportNode(row, deep: true));
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }
}
