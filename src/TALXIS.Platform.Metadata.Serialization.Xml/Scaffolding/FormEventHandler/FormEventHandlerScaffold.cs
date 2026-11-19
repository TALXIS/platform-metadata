using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the JavaScript library on the form (dialogs included) and attaches
/// a handler to the requested event. Creates the event element when missing.
/// </summary>
public static class FormEventHandlerScaffold
{
    public static ScaffoldResult Apply(FormEventHandlerScaffoldRequest request)
    {
        if (IsKeyedByAttribute(request.EventName) && string.IsNullOrWhiteSpace(request.AttributeName))
        {
            throw new ArgumentException($"'{request.EventName}' needs AttributeName: the field logical name for onchange, the button control id for onclick.");
        }

        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntityLogicalName, request.FormType, request.FormId);

        // Registers the library and attaches the event handler.
        AddLibrary(request, formFilePath);
        AddEvent(request, formFilePath);
        return new ScaffoldResult();
    }

    // FormXml stores the field (onchange) or the button control id (onclick) in the event's attribute.
    private static bool IsKeyedByAttribute(string eventName) => eventName is "onchange" or "onclick";

    // Compares values in code instead of building XPath literals, so ids with quotes can't break the query.
    private static IEnumerable<XmlElement> ChildElements(XmlNode parent, string name) =>
        parent.ChildNodes.OfType<XmlElement>().Where(e => e.Name == name);

    private static void AddLibrary(FormEventHandlerScaffoldRequest request, string formFilePath)
    {
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var librariesNode = formDoc.SelectSingleNode("//formLibraries");
        if (librariesNode == null)
        {
            formDoc.SelectSingleNode("//form")!.AppendChild(formDoc.CreateElement("formLibraries"));
            librariesNode = formDoc.SelectSingleNode("//formLibraries")!;
        }

        if (!ChildElements(librariesNode, "Library").Any(l => l.GetAttribute("name") == request.LibraryName))
        {
            var library = formDoc.CreateElement("Library");
            library.SetAttribute("name", request.LibraryName);
            library.SetAttribute("libraryUniqueId", "{" + (request.LibraryUniqueId ?? Guid.NewGuid().ToString()) + "}");
            librariesNode.AppendChild(library);
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
    }

    private static void AddEvent(FormEventHandlerScaffoldRequest request, string formFilePath)
    {
        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var eventsNode = formDoc.SelectSingleNode("//events");
        if (eventsNode == null)
        {
            formDoc.SelectSingleNode("//form")!.AppendChild(formDoc.CreateElement("events"));
            eventsNode = formDoc.SelectSingleNode("//events")!;
        }

        var keyedByAttribute = IsKeyedByAttribute(request.EventName);
        XmlNode? eventNode = ChildElements(eventsNode, "event").FirstOrDefault(e =>
            e.GetAttribute("name") == request.EventName
            && (!keyedByAttribute || e.GetAttribute("attribute") == request.AttributeName));
        if (eventNode == null)
        {
            var created = formDoc.CreateElement("event");
            created.SetAttribute("name", request.EventName);
            created.SetAttribute("application", "false");
            created.SetAttribute("active", "true");
            if (keyedByAttribute) created.SetAttribute("attribute", request.AttributeName!);
            created.AppendChild(formDoc.CreateElement("Handlers"));
            eventsNode.AppendChild(created);
            eventNode = created;
        }

        var handler = formDoc.CreateElement("Handler");
        handler.SetAttribute("libraryName", "$webresource:" + request.LibraryName);
        handler.SetAttribute("functionName", request.FunctionName);
        // Normalizes capitalized boolean input ("True") to the lowercase form used by FormXml.
        handler.SetAttribute("passExecutionContext", request.PassExecutionContext.ToLowerInvariant());
        handler.SetAttribute("enabled", "true");
        handler.SetAttribute("handlerUniqueId", "{" + (request.HandlerUniqueId ?? Guid.NewGuid().ToString()) + "}");
        handler.SetAttribute("parameters", "");
        eventNode.SelectSingleNode("Handlers")!.AppendChild(handler);

        ScaffoldXmlFile.Save(formDoc, formFilePath);
    }
}
