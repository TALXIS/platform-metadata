using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlWorkspaceReaderTests
{
    private const string SamplePath = "/tmp/dpp-sample/sample-repo/src/Solutions.DataModel";

    private static bool SampleRepoExists() => Directory.Exists(SamplePath);

    [Fact]
    public void Load_SampleRepo_ParsesSolution()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.NotNull(workspace.Solution);
        Assert.Equal("SolutionsDataModel", workspace.Solution.UniqueName);
        Assert.Equal("1.0", workspace.Solution.Version);
        Assert.NotNull(workspace.Solution.Publisher);
        Assert.Equal("UDPP", workspace.Solution.Publisher.UniqueName);
        Assert.Equal("udpp", workspace.Solution.Publisher.Prefix);
        Assert.Equal(36171, workspace.Solution.Publisher.OptionValuePrefix);
        Assert.True(workspace.Solution.RootComponents.Count > 0);
    }

    [Fact]
    public void Load_SampleRepo_ParsesEntities()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.True(workspace.Entities.Count > 0);

        var entity = workspace.FindEntity("udpp_warehouseitem");
        Assert.NotNull(entity);
        Assert.Equal("udpp_warehouseitem", entity.LogicalName);
        Assert.Equal("Warehouse Item", entity.DisplayName.Default);
        Assert.Equal("Warehouse Items", entity.PluralName.Default);
        Assert.Equal("udpp_warehouseitems", entity.EntitySetName);
        Assert.Equal(OwnershipType.UserOwned, entity.Ownership);
        Assert.True(entity.IsAuditEnabled);
        Assert.True(entity.IsCustomEntity);
        Assert.True(entity.Attributes.Count > 0);
    }

    [Fact]
    public void Load_SampleRepo_ParsesAttributes()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        var entity = workspace.FindEntity("udpp_warehouseitem");
        Assert.NotNull(entity);

        // Primary key attribute
        Assert.Equal("udpp_warehouseitemid", entity.PrimaryIdAttribute);

        // Primary name attribute
        Assert.Equal("udpp_name", entity.PrimaryNameAttribute);

        // String attribute
        var nameAttr = entity.FindAttribute("udpp_name");
        Assert.NotNull(nameAttr);
        Assert.IsType<StringAttributeMetadata>(nameAttr);
        Assert.True(nameAttr.IsCustomAttribute);
        Assert.Equal("Name", nameAttr.DisplayName.Default);

        // Lookup attribute
        var createdBy = entity.FindAttribute("createdby");
        Assert.NotNull(createdBy);
        Assert.IsType<LookupAttributeMetadata>(createdBy);

        // DateTime attribute
        var createdOn = entity.FindAttribute("createdon");
        Assert.NotNull(createdOn);
        Assert.IsType<DateTimeAttributeMetadata>(createdOn);

        // Primary key maps to UniqueIdentifier
        var pkAttr = entity.FindAttribute("udpp_warehouseitemid");
        Assert.NotNull(pkAttr);
        Assert.IsType<UniqueIdentifierAttributeMetadata>(pkAttr);
    }

    [Fact]
    public void Load_SampleRepo_ParsesGlobalOptionSets()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.True(workspace.GlobalOptionSets.Count > 0);

        var optionSet = workspace.GlobalOptionSets.FirstOrDefault(o => o.Name == "udpp_paymentmethod");
        Assert.NotNull(optionSet);
        Assert.True(optionSet.IsGlobal);
        Assert.Equal("Payment Method", optionSet.DisplayName.Default);
        Assert.Equal(3, optionSet.Options.Count);
        Assert.Equal("Visa", optionSet.Options[0].Label.Default);
        Assert.Equal(687210000, optionSet.Options[0].Value);
    }

    [Fact]
    public void Load_SampleRepo_ParsesRelationships()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.True(workspace.Relationships.Count > 0);
        Assert.Equal("udpp_udpp_warehouseitem_udpp_warehousetransaction_itemid", workspace.Relationships[0].SchemaName);
    }

    [Fact]
    public void Load_SampleRepo_SetsSourceLocation()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.NotNull(workspace.Solution?.Source);
        Assert.Contains("Solution.xml", workspace.Solution.Source.FilePath);

        var entity = workspace.FindEntity("udpp_warehouseitem");
        Assert.NotNull(entity?.Source);
        Assert.Contains("Entity.xml", entity.Source.FilePath);

        Assert.NotNull(workspace.GlobalOptionSets[0].Source);
        Assert.NotNull(workspace.Relationships[0].Source);
    }

    [Fact]
    public void Load_SampleRepo_EntityCountMatchesFolders()
    {
        if (!SampleRepoExists()) return;

        var entityFolderCount = Directory.GetDirectories(Path.Combine(SamplePath, "Entities")).Length;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.Equal(entityFolderCount, workspace.Entities.Count);
    }

    [Fact]
    public void Load_SampleRepo_AttributeTypesCorrectlyMapped()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);
        var entity = workspace.FindEntity("udpp_warehouseitem")!;

        // String (nvarchar)
        Assert.IsType<StringAttributeMetadata>(entity.FindAttribute("udpp_name"));
        // Lookup
        Assert.IsType<LookupAttributeMetadata>(entity.FindAttribute("createdby"));
        // DateTime
        Assert.IsType<DateTimeAttributeMetadata>(entity.FindAttribute("createdon"));
        // UniqueIdentifier (primarykey)
        Assert.IsType<UniqueIdentifierAttributeMetadata>(entity.FindAttribute("udpp_warehouseitemid"));
        // Picklist
        Assert.IsType<PicklistAttributeMetadata>(entity.FindAttribute("udpp_packagetype"));
        // State
        Assert.IsType<StateAttributeMetadata>(entity.FindAttribute("statecode"));
        // Status
        Assert.IsType<StatusAttributeMetadata>(entity.FindAttribute("statuscode"));
        // Integer
        Assert.IsType<IntegerAttributeMetadata>(entity.FindAttribute("importsequencenumber"));
    }

    [Fact]
    public void Load_SampleRepo_PublisherPrefixReadCorrectly()
    {
        if (!SampleRepoExists()) return;

        var reader = new XmlWorkspaceReader();
        var workspace = reader.Load(SamplePath);

        Assert.NotNull(workspace.Solution?.Publisher);
        Assert.Equal("udpp", workspace.Solution.Publisher.Prefix);
        Assert.Equal("UDPP", workspace.Solution.Publisher.UniqueName);
        Assert.Equal(36171, workspace.Solution.Publisher.OptionValuePrefix);
    }

    [Fact]
    public void Load_SampleRepo_RootComponentCountMatchesSolutionXml()
    {
        if (!SampleRepoExists()) return;

        var solutionXml = System.Xml.Linq.XDocument.Load(
            Path.Combine(SamplePath, "Other", "Solution.xml"));
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
    public void Load_EntityWithNoAttributes_EmptyAttributesList()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-noattr-{Guid.NewGuid():N}");
        try
        {
            // Minimal workspace with entity that has no attributes
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            Directory.CreateDirectory(Path.Combine(dir, "Entities", "test_entity"));

            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml version="9.1">
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher>
                      <UniqueName>test</UniqueName>
                      <CustomizationPrefix>test</CustomizationPrefix>
                    </Publisher>
                    <RootComponents />
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
                      <OwnershipTypeMask>UserOwned</OwnershipTypeMask>
                      <IsCustomEntity>1</IsCustomEntity>
                      <attributes />
                    </entity>
                  </EntityInfo>
                </Entity>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(dir);

            var entity = workspace.FindEntity("test_entity");
            Assert.NotNull(entity);
            Assert.Empty(entity.Attributes);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_WorkspaceWithNoOptionSetsFolder_EmptyGlobalOptionSets()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-nooptsets-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml version="9.1">
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher><UniqueName>test</UniqueName><CustomizationPrefix>test</CustomizationPrefix></Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(dir);

            Assert.Empty(workspace.GlobalOptionSets);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_WorkspaceWithNoRelationshipsXml_EmptyRelationships()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"reader-norels-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Other"));
            File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml version="9.1">
                  <SolutionManifest>
                    <UniqueName>TestSolution</UniqueName>
                    <Version>1.0</Version>
                    <Managed>0</Managed>
                    <Publisher><UniqueName>test</UniqueName><CustomizationPrefix>test</CustomizationPrefix></Publisher>
                    <RootComponents />
                  </SolutionManifest>
                </ImportExportXml>
                """);

            var reader = new XmlWorkspaceReader();
            var workspace = reader.Load(dir);

            Assert.Empty(workspace.Relationships);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_NonexistentPath_Throws()
    {
        var reader = new XmlWorkspaceReader();
        Assert.Throws<DirectoryNotFoundException>(() => reader.Load("/nonexistent/path"));
    }

    [Fact]
    public void Load_InvalidPath_ThrowsMeaningfulError()
    {
        var reader = new XmlWorkspaceReader();
        var ex = Assert.Throws<DirectoryNotFoundException>(() => reader.Load("/some/invalid/workspace/path"));
        Assert.Contains("/some/invalid/workspace/path", ex.Message);
    }
}
