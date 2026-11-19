using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Strips unused placeholder nodes from the rendered parameters fragment and
/// appends the parameters element to the control in the targeted cell.
/// </summary>
public static class ControlParameterScaffold
{
    // The placeholder token includes a Cyrillic 'e' (U+0435), which must match exactly.
    private const string UnusedToken = "defaultеtemplateexample";

    public static ScaffoldResult Apply(ControlParameterScaffoldRequest request)
    {
        if (!File.Exists(request.ParametersFilePath))
            throw new FileNotFoundException($"Parameters fragment file not found: {request.ParametersFilePath}");

        var fragmentDoc = ScaffoldXmlFile.Load(request.ParametersFilePath);
        RemoveUnusedNodes(fragmentDoc);

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var section = FormPlacementResolver.ResolveTargetSection(formDoc, request.Placement);
        var row = FormPlacementResolver.ResolveTargetRow(section, request.Placement.RowIndex);

        if (row.SelectSingleNode("./cell[not(@solutionaction='Removed')]") == null)
        {
            throw new InvalidOperationException("No existing cell found in the target row.");
        }
        // Targets the control pp-form-control placed last: the row's last cell holding one.
        var control = row.SelectSingleNode("(./cell[not(@solutionaction='Removed')]/control)[last()]")
            ?? throw new InvalidOperationException("Control node not found in existing cell or parameter path is empty.");

        var parametersNode = formDoc.CreateElement("parameters");
        foreach (XmlNode parameter in fragmentDoc.DocumentElement!.ChildNodes)
        {
            parametersNode.AppendChild(formDoc.ImportNode(parameter, deep: true));
        }
        control.AppendChild(parametersNode);

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }

    private static void RemoveUnusedNodes(XmlDocument fragmentDoc)
    {
        var unused = fragmentDoc.SelectNodes($"//*[normalize-space(text())='{UnusedToken}']")!
            .Cast<XmlNode>()
            .Concat(fragmentDoc.SelectNodes($"//*[normalize-space(text())='{{{UnusedToken}}}']")!.Cast<XmlNode>())
            .ToList();
        foreach (var node in unused)
        {
            node.ParentNode!.RemoveChild(node);
        }
    }
}
