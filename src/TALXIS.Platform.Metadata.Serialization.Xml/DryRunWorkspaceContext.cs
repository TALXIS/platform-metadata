using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Serialization.Xml;

/// <summary>
/// Read-through context that swallows every write and records which files a save would touch; backs <see cref="XmlWorkspaceWriter.GetModifiedFiles(Workspace, string)"/>.
/// </summary>
internal sealed class DryRunWorkspaceContext : IWorkspaceContext
{
    private readonly IWorkspaceContext _inner;
    private readonly List<string> _touched = new();

    public DryRunWorkspaceContext(IWorkspaceContext inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IReadOnlyList<string> TouchedFiles => _touched;

    public bool FileExists(string path) => _inner.FileExists(path);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive) => _inner.EnumerateFiles(directory, searchPattern, recursive);

    public IEnumerable<string> EnumerateDirectories(string directory, bool recursive) => _inner.EnumerateDirectories(directory, recursive);

    public Stream OpenRead(string path) => _inner.OpenRead(path);

    public Stream Create(string path)
    {
        _touched.Add(path);
        return new MemoryStream();
    }

    public void CreateDirectory(string path)
    {
    }

    public void DeleteFile(string path)
    {
        if (_inner.FileExists(path)) _touched.Add(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) => _touched.Add(destinationPath);
}
