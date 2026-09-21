using System.Text.RegularExpressions;
using System.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Stamps the form id into the rendered file, renames it to {id}.xml, and fills
/// the dialog unique name, deriving it from the entity prefix and form name when
/// omitted. Registers the form in Solution.xml (type 60, by id); dialog registrations
/// omit the behavior attribute.
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

        // Loads the XML with default whitespace handling and saves it using Save.
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
