namespace TALXIS.Platform.Metadata.Workspaces;

/// <summary>
/// File I/O boundary for workspace readers and writers: exists, list, read, write, delete. Paths are passed through as given, the model never interprets them.
/// </summary>
public interface IWorkspaceContext
{
    /// <summary>
    /// Returns whether a file exists at the path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Returns whether a directory exists at the path.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Lists files in the directory matching the search pattern, optionally descending into subdirectories; empty for a missing directory.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive);

    /// <summary>
    /// Lists subdirectories of the directory, optionally recursively; empty for a missing directory.
    /// </summary>
    IEnumerable<string> EnumerateDirectories(string directory, bool recursive);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Creates or truncates a file for writing, creating the parent directory when needed.
    /// </summary>
    Stream Create(string path);

    /// <summary>
    /// Creates the directory and any missing parents.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Deletes the file when it exists.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes the directory; a non-empty directory is deleted only when <paramref name="recursive"/> is set.
    /// </summary>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>
    /// Copies a file, creating the destination directory when needed.
    /// </summary>
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
}
