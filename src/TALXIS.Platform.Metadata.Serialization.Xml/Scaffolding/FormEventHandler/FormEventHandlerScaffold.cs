using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-form-event-handler template post-action
/// scripts: registers the script library on the form (skipped for dialogs, like
/// the old AddLibrary.ps1) and attaches a handler to the requested event,
/// creating the event element when missing.
/// Transitional adapter: patches the file directly for byte-compatible output with
/// the old scripts; moves onto the typed workspace model as the manipulation API lands.
/// </summary>
public static class FormEventHandlerScaffold
{
    public static ScaffoldResult Apply(FormEventHandlerScaffoldRequest request)
    {
        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntityLogicalName, request.FormType, request.FormId);

        // The old scripts ran as two post-actions with separate load/save passes.
        AddLibrary(request, formFilePath);
        AddEvent(request, formFilePath);
        return new ScaffoldResult();
    }

    private static void AddLibrary(FormEventHandlerScaffoldRequest request, string formFilePath)
    {
        // Dialog forms carry no formLibraries; the old script exited silently for them.
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
        // The template's choice values render capitalized ("True"); FormXml uses lowercase.
        handler.SetAttribute("passExecutionContext", request.PassExecutionContext.ToLowerInvariant());
        handler.SetAttribute("enabled", "true");
        handler.SetAttribute("handlerUniqueId", "{" + (request.HandlerUniqueId ?? Guid.NewGuid().ToString()) + "}");
        handler.SetAttribute("parameters", "");
        eventNode.SelectSingleNode("Handlers")!.AppendChild(handler);

        ScaffoldXmlFile.Save(formDoc, formFilePath);
    }
}
