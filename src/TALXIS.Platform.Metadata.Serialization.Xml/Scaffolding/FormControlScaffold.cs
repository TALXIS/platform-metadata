using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-control template post-action scripts.
/// Appends the rendered control into the single cell of the resolved row, marking
/// the cell with subgrid spans when the control is a subgrid.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
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

        var cells = row.SelectNodes("./cell")!;
        if (cells.Count == 0) throw new InvalidOperationException("No cells found in the target row.");
        if (cells.Count > 1) throw new InvalidOperationException("Multiple cells found in the target row. Expected only one cell.");
        var cell = (XmlElement)cells[0]!;

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
}
