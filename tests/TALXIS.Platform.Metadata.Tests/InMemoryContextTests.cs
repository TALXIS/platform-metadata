using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

public class InMemoryContextTests
{
    private static readonly string Root = WorkspaceFixtures.VirtualRoot("ws");

    [Fact]
    public void SetFile_ImpliesDirectories_AndEnumerationRespectsPatternAndDepth()
    {
        var context = new InMemoryContext();
        context.SetFile(Path.Combine(Root, "Other", "Solution.xml"), "<a />");
        context.SetFile(Path.Combine(Root, "Entities", "ntg_order", "Entity.xml"), "<b />");
        context.SetFile(Path.Combine(Root, "Entities", "ntg_order", "notes.txt"), "x");

        Assert.True(context.DirectoryExists(Root));
        Assert.True(context.DirectoryExists(Path.Combine(Root, "Entities", "ntg_order")));
        Assert.False(context.DirectoryExists(Path.Combine(Root, "Missing")));
        Assert.Equal(new[] { Path.Combine(Root, "Entities"), Path.Combine(Root, "Other") }, context.EnumerateDirectories(Root, recursive: false));
        Assert.Empty(context.EnumerateFiles(Root, "*.xml", recursive: false));
        Assert.Equal(2, context.EnumerateFiles(Root, "*.xml", recursive: true).Count());
        Assert.Single(context.EnumerateFiles(Path.Combine(Root, "Entities", "ntg_order"), "*.txt", recursive: false));
        Assert.Equal("<b />", context.GetFileText(Path.Combine(Root, "Entities", "ntg_order", "Entity.xml")));
    }

    [Fact]
    public void Create_StoresBytesOnDispose_AndReadsBackThroughOpenRead()
    {
        var context = new InMemoryContext();
        var path = Path.Combine(Root, "new", "file.txt");

        using (var stream = context.Create(path))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("hello");
        }

        using var reader = new StreamReader(context.OpenRead(path));
        Assert.Equal("hello", reader.ReadToEnd());
        Assert.True(context.DirectoryExists(Path.Combine(Root, "new")));
    }

    [Fact]
    public void Delete_And_Copy_FollowFileSystemSemantics()
    {
        var context = new InMemoryContext();
        var file = Path.Combine(Root, "a", "file.txt");
        var copy = Path.Combine(Root, "b", "copy.txt");
        context.SetFile(file, "x");

        context.CopyFile(file, copy, overwrite: false);
        Assert.Throws<IOException>(() => context.CopyFile(file, copy, overwrite: false));
        Assert.Throws<IOException>(() => context.DeleteDirectory(Path.Combine(Root, "a"), recursive: false));

        context.DeleteFile(file);
        context.DeleteFile(file);
        context.DeleteDirectory(Path.Combine(Root, "a"), recursive: false);
        context.DeleteDirectory(Path.Combine(Root, "b"), recursive: true);

        Assert.False(context.FileExists(file));
        Assert.False(context.FileExists(copy));
        Assert.False(context.DirectoryExists(Path.Combine(Root, "a")));
        Assert.Throws<FileNotFoundException>(() => context.OpenRead(file));
    }

    [Fact]
    public void Reader_LoadsAWorkspaceSeededInMemory_WithoutTouchingDisk()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "MemorySolution", "Memory Entity");

        var workspace = new XmlWorkspaceReader(context).Load(Root);

        Assert.Single(workspace.Solutions);
        Assert.Equal("MemorySolution", workspace.Solutions[0].UniqueName);
        Assert.Equal("Memory Entity", workspace.Entities.Single().DisplayName.Default);
        Assert.NotNull(workspace.GetComponent(ComponentType.Entity, "test_entity")!.ActiveState);
    }

    [Fact]
    public void Writer_RoundtripsIntoMemory_AndLoadsBackIdentically()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "MemorySolution", "Memory Entity");
        var output = WorkspaceFixtures.VirtualRoot("out");
        var workspace = new XmlWorkspaceReader(context).Load(Root);

        new XmlWorkspaceWriter(context).Write(workspace, output);
        var reloaded = new XmlWorkspaceReader(context).Load(output);

        Assert.Equal("Memory Entity", reloaded.Entities.Single().DisplayName.Default);
        Assert.Equal(
            WorkspaceFixtures.NormalizeNewlines(context.GetFileText(Path.Combine(Root, "Entities", "test_entity", "Entity.xml"))!),
            WorkspaceFixtures.NormalizeNewlines(context.GetFileText(Path.Combine(output, "Entities", "test_entity", "Entity.xml"))!));
    }
}
