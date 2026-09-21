using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends rendered HideCustomAction entries to the entity ribbon's CustomActions
/// element. Leaves the ribbon unchanged when the payload contains no entries.
/// </summary>
public static class RibbonButtonHideScaffold
{
    public static ScaffoldResult Apply(RibbonButtonHideScaffoldRequest request)
    {
        if (!File.Exists(request.RibbonDiffFilePath))
            throw new FileNotFoundException($"RibbonDiff.xml not found: {request.RibbonDiffFilePath}");

        var hideDoc = new XmlDocument();
        hideDoc.Load(request.HideFilePath);
        var hideActions = hideDoc.SelectNodes("//HideCustomAction")!;
        if (hideActions.Count == 0) return new ScaffoldResult();

        var ribbonDoc = new XmlDocument();
        ribbonDoc.Load(request.RibbonDiffFilePath);
        var customActions = ribbonDoc.SelectSingleNode("//CustomActions")
            ?? throw new InvalidOperationException($"CustomActions node not found in '{request.RibbonDiffFilePath}'.");

        foreach (XmlNode hideAction in hideActions)
        {
            customActions.AppendChild(ribbonDoc.ImportNode(hideAction, true));
        }

        ribbonDoc.Save(request.RibbonDiffFilePath);
        return new ScaffoldResult();
    }
}
