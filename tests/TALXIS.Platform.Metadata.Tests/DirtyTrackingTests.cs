using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Workspaces;

namespace TALXIS.Platform.Metadata.Tests;

public class DirtyTrackingTests
{
    private static readonly string Root = WorkspaceFixtures.VirtualRoot("dirty");

    [Fact]
    public void LoadThenSave_WithoutChanges_WritesNothing()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        using var transaction = new TransactionalContext(context);
        new XmlWorkspaceWriter(transaction).Write(workspace, Root);

        Assert.Empty(modified);
        Assert.Empty(transaction.PendingWrites);
        Assert.Empty(transaction.PendingDeletes);
    }

    [Fact]
    public void ChangingOneLabel_WritesExactlyThatDocument()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var entityFile = Path.Combine(Root, "Entities", "test_entity", "Entity.xml");
        var solutionFile = Path.Combine(Root, "Other", "Solution.xml");
        var solutionBefore = context.GetFileText(solutionFile);

        workspace.Entities[0].DisplayName[1033] = "Renamed";
        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(new[] { entityFile }, modified);
        Assert.Contains("Renamed", context.GetFileText(entityFile));
        Assert.Equal(solutionBefore, context.GetFileText(solutionFile));
    }

    [Fact]
    public void ChangingTheManifest_WritesOnlySolutionXml()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var entityFile = Path.Combine(Root, "Entities", "test_entity", "Entity.xml");
        var entityBefore = context.GetFileText(entityFile);

        workspace.Solutions[0].AddRootComponent(new RootComponent { Type = ComponentType.Role, Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Behavior = 0 });
        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(new[] { Path.Combine(Root, "Other", "Solution.xml") }, modified);
        Assert.Equal(entityBefore, context.GetFileText(entityFile));
        Assert.Contains("11111111-1111-1111-1111-111111111111", context.GetFileText(Path.Combine(Root, "Other", "Solution.xml")));
    }

    [Fact]
    public void MarkDirty_ForcesAWrite_AndAcceptChangesFollowsTheSave()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var entity = workspace.Entities[0];

        entity.MarkDirty();
        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        Assert.True(entity.IsDirty);

        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(new[] { Path.Combine(Root, "Entities", "test_entity", "Entity.xml") }, modified);
        Assert.False(entity.IsDirty);
        Assert.Empty(new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root));
    }

    [Fact]
    public void SecondSaveAfterAChange_IsANoOp()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        workspace.Entities[0].DisplayName[1033] = "Renamed";
        var writer = new XmlWorkspaceWriter(context);

        writer.Write(workspace, Root);

        Assert.Empty(writer.GetModifiedFiles(workspace, Root));
        Assert.Contains("Renamed", context.GetFileText(Path.Combine(Root, "Entities", "test_entity", "Entity.xml")));
    }

    [Fact]
    public void ExportToAnotherDirectory_StillWritesEveryDocument()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var output = WorkspaceFixtures.VirtualRoot("dirty-export");

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, output);
        new XmlWorkspaceWriter(context).Write(workspace, output);

        Assert.Contains(Path.Combine(output, "Other", "Solution.xml"), modified);
        Assert.Contains(Path.Combine(output, "Entities", "test_entity", "Entity.xml"), modified);
        Assert.True(context.FileExists(Path.Combine(output, "Entities", "test_entity", "Entity.xml")));
    }

    [Fact]
    public void NewComponent_IsWrittenEvenThoughNothingElseChanged()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        workspace.AddSecurityRole(new SecurityRoleMetadata { RoleId = "{22222222-2222-2222-2222-222222222222}", Name = "Reviewer" });

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);
        new XmlWorkspaceWriter(context).Write(workspace, Root);

        Assert.Equal(new[] { Path.Combine(Root, "Roles", "Reviewer.xml") }, modified);
        Assert.True(context.FileExists(Path.Combine(Root, "Roles", "Reviewer.xml")));
    }

    [Fact]
    public void DryRun_DoesNotTouchTheContextOrTheDirtyFlags()
    {
        var context = new InMemoryContext();
        WorkspaceFixtures.SeedProject(context, Root, "DirtySolution", "Entity");
        var workspace = new XmlWorkspaceReader(context).Load(Root);
        var before = context.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var entityBefore = context.GetFileText(Path.Combine(Root, "Entities", "test_entity", "Entity.xml"));
        workspace.Entities[0].DisplayName[1033] = "Renamed";
        workspace.Entities[0].MarkDirty();

        var modified = new XmlWorkspaceWriter(context).GetModifiedFiles(workspace, Root);

        Assert.Single(modified);
        Assert.True(workspace.Entities[0].IsDirty);
        Assert.Equal(before, context.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(entityBefore, context.GetFileText(Path.Combine(Root, "Entities", "test_entity", "Entity.xml")));
    }
}
