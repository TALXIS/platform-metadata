using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends the rendered column(s) into the columns container of the resolved tab
/// (or tab footer), creating the container when the tab has none.
/// </summary>
public static class FormColumnScaffold
{
    public static ScaffoldResult Apply(FormColumnScaffoldRequest request)
    {
        if (!File.Exists(request.ColumnFilePath))
            throw new FileNotFoundException($"Column fragment file not found: {request.ColumnFilePath}");

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var container = FormPlacementResolver.ResolveTab(formDoc, request.Placement);

        // Resolves the footer document-wide using //tabfooter.
        if (request.Placement.SetToTabFooter)
        {
            var footers = container.SelectNodes("//tabfooter")!;
            if (footers.Count == 0) throw new InvalidOperationException("Target tab footer not found.");
            container = footers[footers.Count - 1]!;
        }

        var columnsNode = container.SelectSingleNode("./columns");
        if (columnsNode == null)
        {
            columnsNode = formDoc.CreateElement("columns");
            container.AppendChild(columnsNode);
        }

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<columns>" + File.ReadAllText(request.ColumnFilePath) + "</columns>");
        foreach (XmlNode column in fragmentDoc.DocumentElement!.ChildNodes)
        {
            columnsNode.AppendChild(formDoc.ImportNode(column, deep: true));
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }
}
