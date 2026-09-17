using System.Text.RegularExpressions;
using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-entity-form template post-action scripts:
/// stamps the form id into the rendered file, renames it to {id}.xml, fills the
/// dialog unique name (generated from the entity prefix and form name when not
/// given - the old SetVariables.ps1) and registers the form in Solution.xml
/// (type 60, by id; dialogs carry no behavior attribute, like the old script).
/// The old Customizations.xml Dialogs-node patch is deliberately dropped.
/// </summary>
public static class EntityFormScaffold
{
    public static ScaffoldResult Apply(EntityFormScaffoldRequest request)
    {
        var result = new ScaffoldResult();
        var isDialog = string.Equals(request.FormType, "dialog", StringComparison.OrdinalIgnoreCase);
        var formId = request.FormId ?? Guid.NewGuid().ToString();

        if (!File.Exists(request.FormFilePath))
            throw new FileNotFoundException($"Form file not found: {request.FormFilePath}");

        // Default whitespace handling + plain Save match the old script's [xml] cast + .Save().
        var formDoc = new XmlDocument();
        formDoc.Load(request.FormFilePath);

        // Dialogs spell the element FormId; entity forms use lowercase formid.
        var idNode = formDoc.SelectSingleNode(isDialog ? "//FormId" : "//formid");
        if (idNode != null) idNode.InnerText = "{" + formId + "}";
        else result.AddWarning($"formid node not found in '{request.FormFilePath}'.");

        if (isDialog)
        {
            var uniqueName = request.DialogUniqueName ?? GenerateDialogUniqueName(request);
            var uniqueNameNode = formDoc.SelectSingleNode("//UniqueName");
            if (uniqueNameNode != null) uniqueNameNode.InnerText = uniqueName;
            else result.AddWarning($"UniqueName node not found in '{request.FormFilePath}'.");
        }

        var targetPath = Path.Combine(Path.GetDirectoryName(request.FormFilePath)!, "{" + formId + "}.xml");
        formDoc.Save(targetPath);
        if (!string.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(request.FormFilePath), StringComparison.OrdinalIgnoreCase))
            File.Delete(request.FormFilePath);

        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.SystemForm,
            Id = Guid.Parse(formId),
            Behavior = isDialog ? null : 0,
        });

        return result;
    }

    private static string GenerateDialogUniqueName(EntityFormScaffoldRequest request)
    {
        var prefix = (request.EntitySchemaName ?? "").Split('_')[0];
        var name = Regex.Replace(request.FormName ?? "", @"[^\w]", "").ToLowerInvariant();
        return $"{prefix}_{name}dialog";
    }
}
