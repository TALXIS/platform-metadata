namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// Direct-disk <see cref="IWorkspaceContext"/>; the default for scripts, dotnet new and the CLI outside a transaction.
/// </summary>
public sealed class FileSystemContext : IWorkspaceContext
{
    /// <summary>
    /// Shared stateless instance.
    /// </summary>
    public static FileSystemContext Instance { get; } = new();

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        return Directory.GetFiles(directory, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string directory, bool recursive)
    {
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        return Directory.GetDirectories(directory, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    /// <inheritdoc />
    public Stream OpenRead(string path) => File.OpenRead(path);

    /// <inheritdoc />
    public Stream Create(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        return File.Create(path);
    }

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <inheritdoc />
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    /// <inheritdoc />
    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.Copy(sourcePath, destinationPath, overwrite);
    }
}
