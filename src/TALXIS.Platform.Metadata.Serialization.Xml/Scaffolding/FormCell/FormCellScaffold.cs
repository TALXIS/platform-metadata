using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends the rendered cell(s) into the resolved row of the located form,
/// picking the dialog fragment when the form turns out to be a dialog.
/// </summary>
public static class FormCellScaffold
{
    public static ScaffoldResult Apply(FormCellScaffoldRequest request)
    {
        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var fragmentPath = FormScaffoldFragment.PickForForm(formFilePath, request.FormType, request.CellFilePath, request.DialogCellFilePath);

        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var section = FormPlacementResolver.ResolveTargetSection(formDoc, request.Placement);
        var row = FormPlacementResolver.ResolveTargetRow(section, request.Placement.RowIndex);

        var fragmentDoc = new XmlDocument();
        fragmentDoc.LoadXml("<cells>" + File.ReadAllText(fragmentPath) + "</cells>");
        foreach (XmlNode cell in fragmentDoc.DocumentElement!.ChildNodes)
        {
            row.AppendChild(formDoc.ImportNode(cell, deep: true));
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }
}
