namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Typed document store a serializer attaches to a workspace; the document type stays outside this package.
/// </summary>
public sealed class WorkspaceDocumentStore<TDocument> : IWorkspaceDocumentStore where TDocument : class
{
    private readonly Dictionary<string, TDocument> _documents = new();

    /// <inheritdoc />
    public int Count => _documents.Count;

    /// <summary>
    /// Gets the stored document keys.
    /// </summary>
    public IEnumerable<string> Keys => _documents.Keys;

    /// <summary>
    /// Gets or sets the document stored under the key.
    /// </summary>
    public TDocument this[string documentKey]
    {
        get => _documents[documentKey];
        set => _documents[documentKey] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Returns whether a document is stored under the key.
    /// </summary>
    public bool ContainsKey(string documentKey) => _documents.ContainsKey(documentKey);

    /// <summary>
    /// Gets the document stored under the key, when present.
    /// </summary>
    public bool TryGetValue(string documentKey, out TDocument document) => _documents.TryGetValue(documentKey, out document!);

    /// <inheritdoc />
    public bool Remove(string documentKey) => _documents.Remove(documentKey);

    /// <inheritdoc />
    public IWorkspaceDocumentStore CreateEmpty() => new WorkspaceDocumentStore<TDocument>();

    /// <inheritdoc />
    public void CopyFrom(IWorkspaceDocumentStore source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (source is not WorkspaceDocumentStore<TDocument> typed)
            throw new InvalidOperationException($"Cannot copy documents from a store of type '{source.GetType().Name}' into a store of '{typeof(TDocument).Name}' documents.");

        foreach (var document in typed._documents)
        {
            _documents[document.Key] = document.Value;
        }
    }
}
