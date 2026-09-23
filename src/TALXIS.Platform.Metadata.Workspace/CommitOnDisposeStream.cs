namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Write buffer that hands its bytes to a callback exactly once, when disposed; how the buffered contexts implement <see cref="IWorkspaceContext.Create"/>.
/// </summary>
internal sealed class CommitOnDisposeStream : MemoryStream
{
    private readonly Action<byte[]> _onDispose;
    private bool _committed;

    public CommitOnDisposeStream(Action<byte[]> onDispose)
    {
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_committed)
        {
            _committed = true;
            _onDispose(ToArray());
        }

        base.Dispose(disposing);
    }
}
