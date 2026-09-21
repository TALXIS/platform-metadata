using System.Text.RegularExpressions;
using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Derives the button logical name from its label, fills icon placeholders, and
/// creates the entity's RibbonDiff.xml from the empty payload when missing.
/// Appends the rendered command definition, localized labels and custom action
/// to their target elements.
/// </summary>
public static class RibbonButtonScaffold
{
    public static ScaffoldResult Apply(RibbonButtonScaffoldRequest request)
    {
        var logicalName = DeriveLogicalName(request.ButtonLabel);

        var commandDefinition = ReadPayload(request.CommandDefinitionFilePath, logicalName);
        var locLabels = ReadPayload(request.LocLabelsFilePath, logicalName);
        var customAction = ReadPayload(request.CustomActionFilePath, logicalName)
            .Replace("__icon-16x16-placeholder__", IconAttribute("Image16by16", request.Image16by16, "icon16pathdefault"))
            .Replace("__icon-32x32-placeholder__", IconAttribute("Image32by32", request.Image32by32, "icon32pathdefault"))
            .Replace("__modern-image-placeholder__", IconAttribute("ModernImage", request.ModernImage, "modernimagedefault"));

        if (!File.Exists(request.RibbonDiffFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.RibbonDiffFilePath)!);
            File.Copy(request.EmptyRibbonFilePath, request.RibbonDiffFilePath);
        }

        var ribbonDoc = new XmlDocument();
        ribbonDoc.Load(request.RibbonDiffFilePath);

        AppendPayloadChildren(ribbonDoc, "CommandDefinitions", commandDefinition);
        AppendPayloadChildren(ribbonDoc, "LocLabels", locLabels);
        AppendPayloadChildren(ribbonDoc, "CustomActions", customAction);

        ribbonDoc.Save(request.RibbonDiffFilePath);
        return new ScaffoldResult();
    }

    // Label -> logical name: punctuation/symbols stripped, whitespace removed, lowercased.
    internal static string DeriveLogicalName(string buttonLabel)
    {
        var name = Regex.Replace(buttonLabel, @"[\p{P}\p{S}]", "");
        return Regex.Replace(name, @"\s", "").ToLowerInvariant();
    }

    // Escapes bare ampersands in rendered attribute values to produce valid XML,
    // leaving ampersands that belong to existing entity references untouched.
    private static string ReadPayload(string path, string logicalName) =>
        Regex.Replace(
            File.ReadAllText(path).Replace("__button-logical-name__", logicalName),
            @"&(?!(amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)",
            "&amp;");

    private static string IconAttribute(string attributeName, string? webResourceName, string defaultSentinel) =>
        string.IsNullOrEmpty(webResourceName) || webResourceName == defaultSentinel
            ? ""
            : $"{attributeName}=\"$webresource:{webResourceName}\"";

    // Skips payloads whose target element is missing.
    private static void AppendPayloadChildren(XmlDocument ribbonDoc, string targetName, string payloadXml)
    {
        var target = ribbonDoc.SelectSingleNode($"//*[local-name()='{targetName}']");
        if (target == null) return;

        var payloadDoc = new XmlDocument();
        payloadDoc.LoadXml(payloadXml);
        foreach (XmlNode child in payloadDoc.DocumentElement!.ChildNodes)
        {
            target.AppendChild(ribbonDoc.ImportNode(child, true));
        }
    }
}
