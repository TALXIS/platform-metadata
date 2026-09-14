using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the JavaScript library on the form, except for dialogs, and attaches
/// a handler to the requested event. Creates the event element when missing.
/// </summary>
public static class FormEventHandlerScaffold
{
    public static ScaffoldResult Apply(FormEventHandlerScaffoldRequest request)
    {
        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntityLogicalName, request.FormType, request.FormId);

        // Registers the library and attaches the event handler.
        AddLibrary(request, formFilePath);
        AddEvent(request, formFilePath);
        return new ScaffoldResult();
    }

    private static void AddLibrary(FormEventHandlerScaffoldRequest request, string formFilePath)
    {
        // Skips library registration for dialog forms, which have no formLibraries.
        if (new FileInfo(formFilePath).Directory?.Name == "Dialogs") return;

        var formDoc = ScaffoldXmlFile.Load(formFilePath);
        var librariesNode = formDoc.SelectSingleNode("//formLibraries");
        if (librariesNode == null)
        {
            formDoc.SelectSingleNode("//form")!.AppendChild(formDoc.CreateElement("formLibraries"));
            librariesNode = formDoc.SelectSingleNode("//formLibraries")!;
        }

        if (librariesNode.SelectSingleNode($"Library[@name='{request.LibraryName}']") == null)
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

        // onchange events are keyed by name + attribute; the rest by name only.
        var eventXPath = request.EventName == "onchange"
            ? $"event[@name='{request.EventName}' and @attribute='{request.AttributeName}']"
            : $"event[@name='{request.EventName}']";
        var eventNode = eventsNode.SelectSingleNode(eventXPath);
        if (eventNode == null)
        {
            var created = formDoc.CreateElement("event");
            created.SetAttribute("name", request.EventName);
            created.SetAttribute("application", "false");
            created.SetAttribute("active", "true");
            if (request.EventName == "onchange") created.SetAttribute("attribute", request.AttributeName ?? "");
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
