namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// <see cref="IWorkspaceContext"/> that keeps every file in memory; for tests and the language server, where single files are replaced as the editor changes them.
/// </summary>
public sealed class InMemoryContext : IWorkspaceContext
{
    private readonly Dictionary<string, byte[]> _files = new(WorkspacePaths.Comparer);
    private readonly HashSet<string> _directories = new(WorkspacePaths.Comparer);

    /// <summary>
    /// Stores text content under the path, replacing any previous content; UTF-8 without BOM.
    /// </summary>
    public void SetFile(string path, string content) =>
        SetFile(path, new System.Text.UTF8Encoding(false).GetBytes(content ?? throw new ArgumentNullException(nameof(content))));

    /// <summary>
    /// Stores binary content under the path, replacing any previous content.
    /// </summary>
    public void SetFile(string path, byte[] content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        var normalized = WorkspacePaths.Normalize(path);
        _files[normalized] = content;
        
        foreach (var ancestor in WorkspacePaths.Ancestors(normalized)) _directories.Add(ancestor);
    }

    /// <summary>
    /// Reads the stored text under the path, or null when the file does not exist.
    /// </summary>
    public string? GetFileText(string path) =>
        _files.TryGetValue(WorkspacePaths.Normalize(path), out var bytes) ? new System.Text.UTF8Encoding(false).GetString(bytes) : null;

    /// <summary>
    /// All stored file paths.
    /// </summary>
    public IReadOnlyCollection<string> Files => _files.Keys;

    public bool FileExists(string path) => _files.ContainsKey(WorkspacePaths.Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(WorkspacePaths.Normalize(path));

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        var normalized = WorkspacePaths.Normalize(directory);

        return _files.Keys
            .Where(file => recursive ? WorkspacePaths.IsUnder(normalized, file) : WorkspacePaths.IsDirectChild(normalized, file))
            .Where(file => WorkspacePaths.MatchesPattern(Path.GetFileName(file), searchPattern))
            .OrderBy(file => file, WorkspacePaths.Comparer)
            .ToArray();
    }

    public IEnumerable<string> EnumerateDirectories(string directory, bool recursive)
    {
        var normalized = WorkspacePaths.Normalize(directory);

        return _directories
            .Where(dir => recursive ? WorkspacePaths.IsUnder(normalized, dir) : WorkspacePaths.IsDirectChild(normalized, dir))
            .OrderBy(dir => dir, WorkspacePaths.Comparer)
            .ToArray();
    }

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(WorkspacePaths.Normalize(path), out var bytes))
            throw new FileNotFoundException("File not found in the in-memory workspace.", path);

        return new MemoryStream(bytes, writable: false);
    }

    public Stream Create(string path)
    {
        var normalized = WorkspacePaths.Normalize(path);

        return new CommitOnDisposeStream(bytes => SetFile(normalized, bytes));
    }

    public void CreateDirectory(string path)
    {
        var normalized = WorkspacePaths.Normalize(path);
        _directories.Add(normalized);

        foreach (var ancestor in WorkspacePaths.Ancestors(normalized)) _directories.Add(ancestor);
    }

    public void DeleteFile(string path) => _files.Remove(WorkspacePaths.Normalize(path));

    public void DeleteDirectory(string path, bool recursive)
    {
        var normalized = WorkspacePaths.Normalize(path);

        if (!_directories.Contains(normalized))
            throw new DirectoryNotFoundException($"Directory not found in the in-memory workspace: {path}");

        var files = _files.Keys.Where(file => WorkspacePaths.IsUnder(normalized, file)).ToArray();
        var directories = _directories.Where(dir => WorkspacePaths.IsUnder(normalized, dir)).ToArray();

        if (!recursive && (files.Length > 0 || directories.Length > 0))
            throw new IOException($"Directory is not empty: {path}");

        foreach (var file in files) _files.Remove(file);
        foreach (var dir in directories) _directories.Remove(dir);

        _directories.Remove(normalized);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = WorkspacePaths.Normalize(sourcePath);
        var destination = WorkspacePaths.Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var bytes))
            throw new FileNotFoundException("File not found in the in-memory workspace.", sourcePath);
        if (!overwrite && _files.ContainsKey(destination))
            throw new IOException($"File already exists: {destinationPath}");

        SetFile(destination, (byte[])bytes.Clone());
    }
}
