using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-subgrid template post-action script.
/// Appends the rendered subgrid row(s) into the first rows container of the target form.
/// Transitional adapter: patches the file directly for byte-compatible output with the old
/// script; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormSubgridScaffold
{
    public static ScaffoldResult Apply(FormSubgridScaffoldRequest request)
    {
        if (!File.Exists(request.RowFilePath))
            throw new FileNotFoundException($"Row fragment file not found: {request.RowFilePath}");

        var formFilePath = FormXmlLocator.Locate(
            request.SolutionRootPath, request.EntityLogicalName, request.FormType, request.FormId.Trim('{', '}'));

        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var rowsNode = formDoc.SelectSingleNode("//rows")
            ?? throw new InvalidOperationException($"Rows node not found in '{formFilePath}'.");

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
