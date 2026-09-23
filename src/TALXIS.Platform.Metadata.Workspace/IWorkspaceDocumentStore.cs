namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Format-agnostic handle to the source documents a serializer keeps for roundtrip-safe writes, keyed by component document key.
/// </summary>
public interface IWorkspaceDocumentStore
{
    /// <summary>
    /// Gets the number of stored documents.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Removes the document stored under the key, if any.
    /// </summary>
    bool Remove(string documentKey);

    /// <summary>
    /// Creates an empty store of the same document type.
    /// </summary>
    IWorkspaceDocumentStore CreateEmpty();

    /// <summary>
    /// Copies every document from a store of the same document type, overwriting duplicates.
    /// </summary>
    void CopyFrom(IWorkspaceDocumentStore source);
}
