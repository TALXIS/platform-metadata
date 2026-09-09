using System.Text;
using System.Xml;
using TALXIS.Platform.Metadata.Layout;

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

        var formId = request.FormId.Trim('{', '}');
        var formFilePath = Path.Combine(
            request.SolutionRootPath, SolutionPackagerLayout.EntitiesDirectory, request.EntityLogicalName,
            "FormXml", request.FormType, "{" + formId + "}.xml");
        if (!File.Exists(formFilePath))
            throw new FileNotFoundException($"Form file not found: {formFilePath}");

        var formDoc = new XmlDocument();
        formDoc.Load(formFilePath);
        var rowsNode = formDoc.SelectSingleNode("//rows")
            ?? throw new InvalidOperationException($"Rows node not found in '{formFilePath}'.");

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<rows>" + File.ReadAllText(request.RowFilePath) + "</rows>");
        foreach (XmlNode row in fragmentDoc.DocumentElement!.ChildNodes)
        {
            rowsNode.AppendChild(formDoc.ImportNode(row, deep: true));
        }

        SaveXml(formDoc, formFilePath);
        return new ScaffoldResult();
    }

    // Same writer settings as the original script, so output formatting stays byte-compatible.
    private static void SaveXml(XmlDocument doc, string path)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        };
        using var writer = XmlWriter.Create(path, settings);
        doc.Save(writer);
    }
}
