using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends the rendered control into the resolved row (its single cell, or the first
/// empty cell of a multi-cell row), marking
/// the cell with subgrid spans when the control is a subgrid.
/// </summary>
public static class FormControlScaffold
{
    public static ScaffoldResult Apply(FormControlScaffoldRequest request)
    {
        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var fragmentPath = FormScaffoldFragment.PickForForm(formFilePath, request.FormType, request.ControlFilePath, request.DialogControlFilePath);

        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var section = FormPlacementResolver.ResolveTargetSection(formDoc, request.Placement);
        var row = FormPlacementResolver.ResolveTargetRow(section, request.Placement.RowIndex);

        var cell = PickCell(row);

        if (request.RowSpan != null || request.ColumnSpan != null)
        {
            cell.SetAttribute("rowspan", request.RowSpan);
            cell.SetAttribute("colspan", request.ColumnSpan);
            cell.SetAttribute("auto", "false");
        }

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml(File.ReadAllText(fragmentPath));
        cell.AppendChild(formDoc.ImportNode(fragmentDoc.DocumentElement!, deep: true));

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }

    // A single cell keeps the old behavior; in a multi-cell row (e.g. OK + Cancel in a tab footer)
    // each new control fills the next cell that has no control yet.
    private static XmlElement PickCell(XmlNode row)
    {
        var cells = row.SelectNodes("./cell")!;
        if (cells.Count == 0) throw new InvalidOperationException("No cells found in the target row.");
        if (cells.Count == 1) return (XmlElement)cells[0]!;
        return (XmlElement?)row.SelectSingleNode("./cell[not(control)]")
            ?? throw new InvalidOperationException("Every cell in the target row already holds a control. Add a cell or target another row.");
    }
}
