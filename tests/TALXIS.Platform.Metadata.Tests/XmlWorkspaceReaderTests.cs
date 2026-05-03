using TALXIS.Platform.Metadata;
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
    public void Load_PreservesDistinctDataverseRequiredLevels()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-requiredlevel-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "required_entity"));

            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>T</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>t</UniqueName>
                      <CustomizationPrefix>t</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            File.WriteAllText(Path.Combine(dir, "Entities", "required_entity", "Entity.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Entity>
                  <EntityInfo>
                    <entity Name="required_entity">
                      <EntitySetName>required_entities</EntitySetName>
                      <LocalizedNames><LocalizedName description="Required Entity" languagecode="1033" /></LocalizedNames>
                      <LocalizedCollectionNames><LocalizedCollectionName description="Required Entities" languagecode="1033" /></LocalizedCollectionNames>
                      <attributes>
                        <attribute PhysicalName="app_required">
                          <Type>nvarchar</Type>
                          <LogicalName>app_required</LogicalName>
                          <RequiredLevel>applicationrequired</RequiredLevel>
                        </attribute>
                        <attribute PhysicalName="sys_required">
                          <Type>nvarchar</Type>
                          <LogicalName>sys_required</LogicalName>
                          <RequiredLevel>systemrequired</RequiredLevel>
                        </attribute>
                        <attribute PhysicalName="required">
                          <Type>nvarchar</Type>
                          <LogicalName>required</LogicalName>
                          <RequiredLevel>required</RequiredLevel>
                        </attribute>
                      </attributes>
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);
            var entity = workspace.FindEntity("required_entity")!;

            Assert.Equal(RequiredLevel.ApplicationRequired, entity.FindAttribute("app_required")!.RequiredLevel);
            Assert.Equal(RequiredLevel.SystemRequired, entity.FindAttribute("sys_required")!.RequiredLevel);
            Assert.Equal(RequiredLevel.Required, entity.FindAttribute("required")!.RequiredLevel);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_PreservesRawManagedValue()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-managed-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>T</UniqueName>
                    <Version>1.0</Version>
                    <Managed>2</Managed>
                    <Publisher>
                      <UniqueName>t</UniqueName>
                      <CustomizationPrefix>t</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            var workspace = new XmlWorkspaceReader().Load(dir);

            Assert.Equal("2", workspace.Solution!.ManagedValue);
            Assert.True(workspace.Solution.IsManaged);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
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
    public void Load_SourceLocations_UseElementLineInfo()
    {
        var workspace = new XmlWorkspaceReader().Load(SamplePath);

        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Other", "Solution.xml"), 3, 4), workspace.Solution!.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Other", "Solution.xml"), 11, 6), workspace.Solution.Publisher!.Source);

        var entity = workspace.FindEntity("test_entity")!;
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 4, 6), entity.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 15, 10), entity.FindAttribute("tp_name")!.Source);
        Assert.Equal(new SourceLocation(Path.Combine(SamplePath, "Entities", "test_entity", "Entity.xml"), 40, 10), entity.FindAttribute("tp_count")!.Source);
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
