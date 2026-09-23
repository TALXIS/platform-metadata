using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

public class TransactionalContextTests
{
    private static readonly string Root = WorkspaceFixtures.VirtualRoot("repo");

    [Fact]
    public void Writes_AreVisibleInsideTheTransaction_ButNotInTheInnerContext_UntilCommit()
    {
        var inner = new InMemoryContext();
        var path = Path.Combine(Root, "Other", "Solution.xml");
        using var transaction = new TransactionalContext(inner);

        WriteText(transaction, path, "<a />");

        Assert.True(transaction.FileExists(path));
        Assert.True(transaction.DirectoryExists(Path.Combine(Root, "Other")));
        Assert.Single(transaction.EnumerateFiles(Path.Combine(Root, "Other"), "*.xml", recursive: false));
        Assert.Equal("<a />", ReadText(transaction, path));
        Assert.False(inner.FileExists(path));
        Assert.True(transaction.HasPendingChanges);

        transaction.Commit();

        Assert.Equal("<a />", inner.GetFileText(path));
        Assert.False(transaction.HasPendingChanges);
        Assert.Throws<InvalidOperationException>(() => transaction.Create(path));
    }

    [Fact]
    public void Rollback_AndDisposeWithoutCommit_DiscardEverything()
    {
        var inner = new InMemoryContext();
        var existing = Path.Combine(Root, "keep.txt");
        inner.SetFile(existing, "keep");

        using (var transaction = new TransactionalContext(inner))
        {
            WriteText(transaction, Path.Combine(Root, "new.txt"), "new");
            transaction.DeleteFile(existing);
            Assert.False(transaction.FileExists(existing));

            transaction.Rollback();

            Assert.False(transaction.HasPendingChanges);
            Assert.True(transaction.FileExists(existing));
            WriteText(transaction, Path.Combine(Root, "other.txt"), "other");
        }

        Assert.Equal(new[] { existing }, inner.Files);
    }

    [Fact]
    public void Deletes_HideInnerFiles_AndCommitAppliesDeletesBeforeWrites()
    {
        var inner = new InMemoryContext();
        var stale = Path.Combine(Root, "Other", "Relationships", "old.xml");
        var replaced = Path.Combine(Root, "Other", "Solution.xml");
        inner.SetFile(stale, "old");
        inner.SetFile(replaced, "v1");
        using var transaction = new TransactionalContext(inner);

        transaction.DeleteFile(stale);
        transaction.DeleteDirectory(Path.Combine(Root, "Other", "Relationships"), recursive: false);
        WriteText(transaction, replaced, "v2");

        Assert.False(transaction.FileExists(stale));
        Assert.False(transaction.DirectoryExists(Path.Combine(Root, "Other", "Relationships")));
        Assert.Equal(new[] { replaced }, transaction.EnumerateFiles(Path.Combine(Root, "Other"), "*.xml", recursive: true));
        Assert.Equal("v1", inner.GetFileText(replaced));

        transaction.Commit();

        Assert.False(inner.FileExists(stale));
        Assert.False(inner.DirectoryExists(Path.Combine(Root, "Other", "Relationships")));
        Assert.Equal("v2", inner.GetFileText(replaced));
    }

    [Fact]
    public void DeleteDirectory_RefusesNonEmptyDirectory_UnlessRecursive()
    {
        var inner = new InMemoryContext();
        inner.SetFile(Path.Combine(Root, "dir", "a.txt"), "a");
        using var transaction = new TransactionalContext(inner);
        WriteText(transaction, Path.Combine(Root, "dir", "b.txt"), "b");

        Assert.Throws<IOException>(() => transaction.DeleteDirectory(Path.Combine(Root, "dir"), recursive: false));

        transaction.DeleteDirectory(Path.Combine(Root, "dir"), recursive: true);
        transaction.Commit();

        Assert.Empty(inner.Files);
    }

    [Fact]
    public void WriterThroughTransaction_UpdatesTwoProjectsAtomically()
    {
        var inner = new InMemoryContext();
        var dataModel = Path.Combine(Root, "Solutions.DataModel");
        var ui = Path.Combine(Root, "Solutions.UI");
        WorkspaceFixtures.SeedProject(inner, dataModel, "Solutions.DataModel", "Data Entity");
        WorkspaceFixtures.SeedProject(inner, ui, "Solutions.UI", "UI Entity");
        var before = inner.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var reader = new XmlWorkspaceReader(inner);
        var dataModelWorkspace = reader.Load(dataModel);
        var uiWorkspace = reader.Load(ui);
        dataModelWorkspace.Entities[0].DisplayName[1033] = "Data Entity v2";
        uiWorkspace.Entities[0].DisplayName[1033] = "UI Entity v2";

        using (var transaction = new TransactionalContext(inner))
        {
            var writer = new XmlWorkspaceWriter(transaction);
            writer.Write(dataModelWorkspace, dataModel);
            writer.Write(uiWorkspace, ui);

            Assert.Equal(before, inner.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Data Entity", inner.GetFileText(Path.Combine(dataModel, "Entities", "test_entity", "Entity.xml")));
            Assert.Equal(2, transaction.PendingWrites.Count(path => path.EndsWith("Entity.xml", StringComparison.OrdinalIgnoreCase)));

            transaction.Commit();
        }

        Assert.Contains("Data Entity v2", inner.GetFileText(Path.Combine(dataModel, "Entities", "test_entity", "Entity.xml")));
        Assert.Contains("UI Entity v2", inner.GetFileText(Path.Combine(ui, "Entities", "test_entity", "Entity.xml")));
        Assert.Equal("Data Entity v2", new XmlWorkspaceReader(inner).Load(dataModel).Entities[0].DisplayName.Default);
    }

    [Fact]
    public void FailedWriteInsideTransaction_LeavesInnerUntouched()
    {
        var inner = new InMemoryContext();
        var project = Path.Combine(Root, "Solutions.DataModel");
        WorkspaceFixtures.SeedProject(inner, project, "Solutions.DataModel", "Data Entity");
        var before = inner.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var workspace = new XmlWorkspaceReader(inner).Load(project);
        workspace.Entities[0].DisplayName[1033] = "changed";

        using (var transaction = new TransactionalContext(inner))
        {
            new XmlWorkspaceWriter(transaction).Write(workspace, project);
            Assert.True(transaction.HasPendingChanges);
        }

        Assert.Equal(before, inner.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Data Entity", inner.GetFileText(Path.Combine(project, "Entities", "test_entity", "Entity.xml")));
        Assert.DoesNotContain("changed", inner.GetFileText(Path.Combine(project, "Entities", "test_entity", "Entity.xml")));
    }

    private static void WriteText(IWorkspaceContext context, string path, string content)
    {
        using var stream = context.Create(path);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static string ReadText(IWorkspaceContext context, string path)
    {
        using var reader = new StreamReader(context.OpenRead(path));
        return reader.ReadToEnd();
    }
}
