using System.Xml.Linq;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Attaches the XML roundtrip document cache to a workspace; the workspace itself stays XML-free.
/// </summary>
internal static class WorkspaceXmlDocuments
{
    /// <summary>
    /// Original XML documents stored by the reader for roundtrip-safe writing, created on first use.
    /// Keys include "Solution:{uniqueName}:Solution.xml", "Entity:{logicalName}", "OptionSet:{name}", "Relationships.xml".
    /// </summary>
    public static WorkspaceDocumentStore<XDocument> OriginalDocuments(this Workspace workspace)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));
        if (workspace.Documents is WorkspaceDocumentStore<XDocument> store) return store;
        if (workspace.Documents != null)
            throw new InvalidOperationException($"Workspace documents are owned by '{workspace.Documents.GetType().Name}', not by the XML serializer.");

        store = new WorkspaceDocumentStore<XDocument>();
        workspace.Documents = store;
        return store;
    }
}
