namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Buffered overlay over another <see cref="IWorkspaceContext"/>: reads fall through, writes and deletes wait in memory until <see cref="Commit"/>; disposing without committing discards them.
/// </summary>
public sealed class TransactionalContext : IWorkspaceContext, IDisposable
{
    private readonly IWorkspaceContext _inner;
    private readonly Dictionary<string, byte[]> _writes = new(WorkspacePaths.Comparer);
    private readonly HashSet<string> _createdDirectories = new(WorkspacePaths.Comparer);
    private readonly HashSet<string> _deletedFiles = new(WorkspacePaths.Comparer);
    private readonly Dictionary<string, bool> _deletedDirectories = new(WorkspacePaths.Comparer);
    private bool _committed;

    /// <summary>
    /// Creates a transaction over the supplied context.
    /// </summary>
    public TransactionalContext(IWorkspaceContext inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Whether any write or delete is waiting for <see cref="Commit"/>.
    /// </summary>
    public bool HasPendingChanges => _writes.Count > 0 || _createdDirectories.Count > 0 || _deletedFiles.Count > 0 || _deletedDirectories.Count > 0;

    /// <summary>
    /// Files that will be written on commit.
    /// </summary>
    public IReadOnlyCollection<string> PendingWrites => _writes.Keys;

    /// <summary>
    /// Files that will be deleted on commit.
    /// </summary>
    public IReadOnlyCollection<string> PendingDeletes => _deletedFiles;

    /// <summary>
    /// Applies deletes first, then directories, then writes to the inner context and clears the buffer; the transaction cannot be used afterwards.
    /// </summary>
    public void Commit()
    {
        ThrowIfCommitted();

        foreach (var file in _deletedFiles) _inner.DeleteFile(file);
        foreach (var directory in _deletedDirectories.OrderByDescending(pair => pair.Key.Length))
        {
            if (_inner.DirectoryExists(directory.Key)) _inner.DeleteDirectory(directory.Key, directory.Value);
        }
        foreach (var directory in _createdDirectories.OrderBy(path => path.Length)) _inner.CreateDirectory(directory);
        foreach (var write in _writes)
        {
            using var stream = _inner.Create(write.Key);
            stream.Write(write.Value, 0, write.Value.Length);
        }

        Clear();
        _committed = true;
    }

    /// <summary>
    /// Discards every buffered change; the transaction stays usable.
    /// </summary>
    public void Rollback() => Clear();

    /// <summary>
    /// Discards buffered changes unless <see cref="Commit"/> ran.
    /// </summary>
    public void Dispose()
    {
        if (!_committed) Clear();
    }


    public bool FileExists(string path)
    {
        var normalized = WorkspacePaths.Normalize(path);
        if (_writes.ContainsKey(normalized)) return true;
        if (IsDeleted(normalized)) return false;
        return _inner.FileExists(normalized);
    }


    public bool DirectoryExists(string path)
    {
        var normalized = WorkspacePaths.Normalize(path);
        if (_createdDirectories.Contains(normalized) || _writes.Keys.Any(file => WorkspacePaths.IsUnder(normalized, file))) return true;
        if (IsUnderDeletedDirectory(normalized) || _deletedDirectories.ContainsKey(normalized)) return false;
        return _inner.DirectoryExists(normalized);
    }


    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        var normalized = WorkspacePaths.Normalize(directory);
        var visible = _inner.EnumerateFiles(normalized, searchPattern, recursive)
            .Select(WorkspacePaths.Normalize)
            .Where(file => !IsDeleted(file));
        var pending = _writes.Keys
            .Where(file => recursive ? WorkspacePaths.IsUnder(normalized, file) : WorkspacePaths.IsDirectChild(normalized, file))
            .Where(file => WorkspacePaths.MatchesPattern(Path.GetFileName(file), searchPattern));
        return visible.Concat(pending).Distinct(WorkspacePaths.Comparer).OrderBy(file => file, WorkspacePaths.Comparer).ToArray();
    }


    public IEnumerable<string> EnumerateDirectories(string directory, bool recursive)
    {
        var normalized = WorkspacePaths.Normalize(directory);
        var visible = _inner.EnumerateDirectories(normalized, recursive)
            .Select(WorkspacePaths.Normalize)
            .Where(dir => !_deletedDirectories.ContainsKey(dir) && !IsUnderDeletedDirectory(dir));
        var pending = _createdDirectories
            .Concat(_writes.Keys.SelectMany(WorkspacePaths.Ancestors))
            .Where(dir => recursive ? WorkspacePaths.IsUnder(normalized, dir) : WorkspacePaths.IsDirectChild(normalized, dir));
        return visible.Concat(pending).Distinct(WorkspacePaths.Comparer).OrderBy(dir => dir, WorkspacePaths.Comparer).ToArray();
    }


    public Stream OpenRead(string path)
    {
        var normalized = WorkspacePaths.Normalize(path);
        if (_writes.TryGetValue(normalized, out var pending)) return new MemoryStream(pending, writable: false);
        if (IsDeleted(normalized)) throw new FileNotFoundException("File was deleted in this transaction.", path);
        return _inner.OpenRead(normalized);
    }


    public Stream Create(string path)
    {
        ThrowIfCommitted();
        var normalized = WorkspacePaths.Normalize(path);
        return new CommitOnDisposeStream(bytes =>
        {
            _deletedFiles.Remove(normalized);
            foreach (var ancestor in WorkspacePaths.Ancestors(normalized)) _deletedDirectories.Remove(ancestor);
            _writes[normalized] = bytes;
        });
    }


    public void CreateDirectory(string path)
    {
        ThrowIfCommitted();
        var normalized = WorkspacePaths.Normalize(path);
        _deletedDirectories.Remove(normalized);
        foreach (var ancestor in WorkspacePaths.Ancestors(normalized)) _deletedDirectories.Remove(ancestor);
        if (!_inner.DirectoryExists(normalized)) _createdDirectories.Add(normalized);
    }


    public void DeleteFile(string path)
    {
        ThrowIfCommitted();
        var normalized = WorkspacePaths.Normalize(path);
        _writes.Remove(normalized);
        if (_inner.FileExists(normalized)) _deletedFiles.Add(normalized);
    }


    public void DeleteDirectory(string path, bool recursive)
    {
        ThrowIfCommitted();
        var normalized = WorkspacePaths.Normalize(path);
        if (!DirectoryExists(normalized))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var hasEntries = EnumerateFiles(normalized, "*", recursive: true).Any() || EnumerateDirectories(normalized, recursive: true).Any();
        if (!recursive && hasEntries)
            throw new IOException($"Directory is not empty: {path}");

        foreach (var file in _writes.Keys.Where(file => WorkspacePaths.IsUnder(normalized, file)).ToArray()) _writes.Remove(file);
        foreach (var dir in _createdDirectories.Where(dir => WorkspacePaths.IsUnder(normalized, dir)).ToArray()) _createdDirectories.Remove(dir);
        _createdDirectories.Remove(normalized);
        if (_inner.DirectoryExists(normalized)) _deletedDirectories[normalized] = recursive;
    }


    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        ThrowIfCommitted();
        if (!overwrite && FileExists(destinationPath))
            throw new IOException($"File already exists: {destinationPath}");

        using var source = OpenRead(sourcePath);
        using var destination = Create(destinationPath);
        source.CopyTo(destination);
    }

    private bool IsDeleted(string normalizedFile) => _deletedFiles.Contains(normalizedFile) || IsUnderDeletedDirectory(normalizedFile);

    private bool IsUnderDeletedDirectory(string normalizedPath) =>
        _deletedDirectories.Keys.Any(dir => WorkspacePaths.IsUnder(dir, normalizedPath));

    private void Clear()
    {
        _writes.Clear();
        _createdDirectories.Clear();
        _deletedFiles.Clear();
        _deletedDirectories.Clear();
    }

    private void ThrowIfCommitted()
    {
        if (_committed) throw new InvalidOperationException("The transaction was already committed.");
    }
}
