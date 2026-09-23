using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

public class WorkspaceContextTests
{
    [Fact]
    public void FileSystemContext_EnumeratesNothing_ForMissingDirectory()
    {
        var context = FileSystemContext.Instance;
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        Assert.False(context.DirectoryExists(missing));
        Assert.Empty(context.EnumerateFiles(missing, "*.xml", recursive: true));
        Assert.Empty(context.EnumerateDirectories(missing, recursive: false));
    }

    [Fact]
    public void FileSystemContext_CreateAndCopy_CreateParentDirectories_AndDeleteIsIdempotent()
    {
        var root = NewTempDir();
        try
        {
            var context = FileSystemContext.Instance;
            var file = Path.Combine(root, "a", "b", "file.txt");
            var copy = Path.Combine(root, "c", "copy.txt");

            using (var stream = context.Create(file))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("hello");
            }
            context.CopyFile(file, copy, overwrite: true);

            Assert.True(context.FileExists(file));
            Assert.True(context.FileExists(copy));
            Assert.Equal("hello", File.ReadAllText(copy));
            Assert.Equal(new[] { Path.Combine(root, "a"), Path.Combine(root, "c") }, context.EnumerateDirectories(root, recursive: false).OrderBy(p => p));
            Assert.Equal(2, context.EnumerateFiles(root, "*.txt", recursive: true).Count());

            context.DeleteFile(copy);
            context.DeleteFile(copy);
            context.DeleteDirectory(Path.Combine(root, "c"), recursive: false);

            Assert.False(context.FileExists(copy));
            Assert.False(context.DirectoryExists(Path.Combine(root, "c")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Reader_PerformsAllFileAccessThroughTheContext()
    {
        var root = NewTempDir();
        try
        {
            var project = WriteProject(root, "ContextSolution");
            var recording = new RecordingContext();

            var workspace = new XmlWorkspaceReader(recording).Load(project);

            Assert.Single(workspace.Entities);
            Assert.NotEmpty(recording.Reads);
            Assert.All(recording.Touched, path => Assert.StartsWith(project, path, StringComparison.OrdinalIgnoreCase));
            Assert.Empty(recording.Writes);
            Assert.Contains(recording.Reads, path => path.EndsWith("Entity.xml", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(recording.Reads, path => path.EndsWith("Solution.xml", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Writer_PerformsAllFileAccessThroughTheContext()
    {
        var root = NewTempDir();
        try
        {
            var project = WriteProject(root, "ContextSolution");
            var output = Path.Combine(root, "out");
            var workspace = new XmlWorkspaceReader().Load(project);
            var recording = new RecordingContext();

            new XmlWorkspaceWriter(recording).Write(workspace, output);

            Assert.NotEmpty(recording.Writes);
            Assert.All(recording.Writes, path => Assert.StartsWith(output, path, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(output, "Other", "Solution.xml")));
            Assert.True(File.Exists(Path.Combine(output, "Entities", "test_entity", "Entity.xml")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultConstructors_UseTheFileSystem()
    {
        var root = NewTempDir();
        try
        {
            var project = WriteProject(root, "ContextSolution");
            var output = Path.Combine(root, "out");

            var workspace = new XmlWorkspaceReader().Load(project);
            new XmlWorkspaceWriter().Write(workspace, output);

            Assert.Equal(
                Normalize(File.ReadAllText(Path.Combine(project, "Entities", "test_entity", "Entity.xml"))),
                Normalize(File.ReadAllText(Path.Combine(output, "Entities", "test_entity", "Entity.xml"))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingContext : IWorkspaceContext
    {
        private readonly IWorkspaceContext _inner = FileSystemContext.Instance;

        public List<string> Reads { get; } = new();
        public List<string> Writes { get; } = new();
        public List<string> Touched { get; } = new();

        public bool FileExists(string path) => Track(_inner.FileExists(path), path);
        public bool DirectoryExists(string path) => Track(_inner.DirectoryExists(path), path);
        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive) => Track(_inner.EnumerateFiles(directory, searchPattern, recursive), directory);
        public IEnumerable<string> EnumerateDirectories(string directory, bool recursive) => Track(_inner.EnumerateDirectories(directory, recursive), directory);

        public Stream OpenRead(string path)
        {
            Reads.Add(path);
            return Track(_inner.OpenRead(path), path);
        }

        public Stream Create(string path)
        {
            Writes.Add(path);
            return Track(_inner.Create(path), path);
        }

        public void CreateDirectory(string path)
        {
            Writes.Add(path);
            Track(0, path);
            _inner.CreateDirectory(path);
        }

        public void DeleteFile(string path)
        {
            Writes.Add(path);
            _inner.DeleteFile(path);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            Writes.Add(path);
            _inner.DeleteDirectory(path, recursive);
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            Reads.Add(sourcePath);
            Writes.Add(destinationPath);
            _inner.CopyFile(sourcePath, destinationPath, overwrite);
        }

        private T Track<T>(T result, string path)
        {
            Touched.Add(path);
            return result;
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"context-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteProject(string root, string uniqueName)
    {
        var dir = Path.Combine(root, uniqueName);
        Directory.CreateDirectory(Path.Combine(dir, "Other"));
        Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));
        File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml version="9.1">
              <SolutionManifest>
                <UniqueName>{uniqueName}</UniqueName>
                <Version>1.0</Version>
                <Managed>0</Managed>
                <Publisher>
                  <UniqueName>test</UniqueName>
                  <CustomizationPrefix>test</CustomizationPrefix>
                </Publisher>
                <RootComponents>
                  <RootComponent type="1" schemaName="test_entity" behavior="0" />
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);
        File.WriteAllText(Path.Combine(dir, "Entities", "test_entity", "Entity.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity>
              <EntityInfo>
                <entity Name="test_entity">
                  <EntitySetName>test_entities</EntitySetName>
                  <LocalizedNames><LocalizedName description="Test Entity" languagecode="1033" /></LocalizedNames>
                  <LocalizedCollectionNames><LocalizedCollectionName description="Test Entities" languagecode="1033" /></LocalizedCollectionNames>
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """);
        return dir;
    }
}
