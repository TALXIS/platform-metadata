using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-app-model-component template post-action
/// script: appends an AppModuleComponent entry to the app's AppModule.xml -
/// entities (type 1) by schema name, everything else by braced id. Like the old
/// script, a missing AppModule.xml is a warning, not an error.
/// </summary>
public static class AppModelComponentScaffold
{
    public static ScaffoldResult Apply(AppModelComponentScaffoldRequest request)
    {
        var result = new ScaffoldResult();
        var appModulePath = ManagedXmlFileLocator.Locate(request.AppModuleFilePath);
        if (appModulePath == null)
        {
            result.AddWarning($"No AppModule XML found at '{request.AppModuleFilePath}' - skipping component registration.");
            return result;
        }

        var doc = new XmlDocument();
        doc.Load(appModulePath);
        var components = doc.SelectSingleNode("//AppModuleComponents")
            ?? throw new InvalidOperationException($"AppModuleComponents node not found in '{appModulePath}'.");

        var component = doc.CreateElement("AppModuleComponent");
        component.SetAttribute("type", request.ComponentTypeId);
        if (request.ComponentTypeId == "1")
            component.SetAttribute("schemaName", request.EntitySchemaName ?? "");
        else
            component.SetAttribute("id", "{" + request.ComponentId + "}");
        components.AppendChild(component);

        doc.Save(appModulePath);
        return result;
    }
}
