using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Appends an AppModuleComponent entry to the app's AppModule.xml: entities
/// (type 1) use schema names and other types use braced ids. Reports a warning
/// when AppModule.xml is missing.
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
