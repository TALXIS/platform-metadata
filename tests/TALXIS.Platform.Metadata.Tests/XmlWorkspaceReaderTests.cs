using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlWorkspaceReaderTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWorkspace");

    [Fact]
    public void Load_ParsesSolution()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.NotNull(workspace.Solution);
        Assert.Equal("TestSolution", workspace.Solution.UniqueName);
        Assert.Equal("1.0.0.0", workspace.Solution.Version);
        Assert.NotNull(workspace.Solution.Publisher);
        Assert.Equal("TestPub", workspace.Solution.Publisher.UniqueName);
        Assert.Equal("tp", workspace.Solution.Publisher.Prefix);
        Assert.Equal(10000, workspace.Solution.Publisher.OptionValuePrefix);
        Assert.Single(workspace.Solution.RootComponents);
    }

    [Fact]
    public void Load_ParsesEntities()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.Single(workspace.Entities);
        var entity = workspace.FindEntity("test_entity");
        Assert.NotNull(entity);
        Assert.Equal("test_entity", entity.LogicalName);
        Assert.Equal("Test Entity", entity.DisplayName.Default);
        Assert.Equal("Test Entities", entity.PluralName.Default);
        Assert.Equal("test_entities", entity.EntitySetName);
        Assert.True(entity.Attributes.Count >= 2);
    }

    [Fact]
    public void Load_ParsesAttributes()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var entity = workspace.FindEntity("test_entity")!;

        var nameAttr = entity.FindAttribute("tp_name");
        Assert.NotNull(nameAttr);
        Assert.IsType<StringAttributeMetadata>(nameAttr);
        Assert.Equal("Name", nameAttr.DisplayName.Default);

        var countAttr = entity.FindAttribute("tp_count");
        Assert.NotNull(countAttr);
        Assert.IsType<IntegerAttributeMetadata>(countAttr);
    }

    [Fact]
    public void Load_ParsesGlobalOptionSets()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.Single(workspace.GlobalOptionSets);
        var os = workspace.GlobalOptionSets[0];
        Assert.Equal("tp_teststatus", os.Name);
        Assert.True(os.IsGlobal);
        Assert.Equal(2, os.Options.Count);
        Assert.Equal("Active", os.Options[0].Label.Default);
        Assert.Equal(100000000, os.Options[0].Value);
    }

    [Fact]
    public void Load_SetsSourceLocation()
    {
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.NotNull(workspace.Solution?.Source);
        Assert.Contains("Solution.xml", workspace.Solution.Source.FilePath);

        var entity = workspace.FindEntity("test_entity");
        Assert.NotNull(entity?.Source);
        Assert.Contains("Entity.xml", entity.Source.FilePath);

        Assert.NotNull(workspace.GlobalOptionSets[0].Source);
    }

    [Fact]
    public void Load_EntityCountMatchesFolders()
    {
        var entityFolderCount = Directory.GetDirectories(Path.Combine(SamplePath, "Entities")).Length;
        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        Assert.Equal(entityFolderCount, workspace.Entities.Count);
    }

    [Fact]
    public void Load_RootComponentCountMatchesSolutionXml()
    {
        var solutionXml = System.Xml.Linq.XDocument.Load(Path.Combine(SamplePath, "Other", "Solution.xml"));
        var expectedCount = solutionXml.Root!
            .Element("SolutionManifest")!
            .Element("RootComponents")!
            .Elements("RootComponent")
            .Count();

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        Assert.Equal(expectedCount, workspace.Solution!.RootComponents.Count);
    }

    [Fact]
    public void Load_EntityWithNoAttributes_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "bare_entity"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");
            File.WriteAllText(Path.Combine(dir, "Entities", "bare_entity", "Entity.xml"),
                "<?xml version=\"1.0\"?><Entity><EntityInfo><entity Name=\"bare_entity\"><EntitySetName>bare_entities</EntitySetName><LocalizedNames><LocalizedName description=\"Bare\" languagecode=\"1033\"/></LocalizedNames><LocalizedCollectionNames><LocalizedCollectionName description=\"Bares\" languagecode=\"1033\"/></LocalizedCollectionNames><attributes/></entity></EntityInfo></Entity>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.FindEntity("bare_entity")!.Attributes);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_NoOptionSetsFolder_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.GlobalOptionSets);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_NoRelationshipsXml_EmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                "<?xml version=\"1.0\"?><ImportExportXml><SolutionManifest><UniqueName>T</UniqueName><Version>1.0</Version><Managed>0</Managed><Publisher><UniqueName>t</UniqueName><CustomizationPrefix>t</CustomizationPrefix></Publisher><RootComponents/></SolutionManifest></ImportExportXml>");

            var workspace = new XmlWorkspaceReader().Load(dir);
            Assert.Empty(workspace.Relationships);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_InvalidPath_ThrowsWithPath()
    {
        var ex = Assert.Throws<DirectoryNotFoundException>(() => new XmlWorkspaceReader().Load("/nonexistent/path"));
        Assert.Contains("/nonexistent/path", ex.Message);
    }
}
