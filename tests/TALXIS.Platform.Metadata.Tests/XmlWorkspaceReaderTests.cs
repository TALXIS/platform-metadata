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
    public void Load_NonexistentPath_Throws()
    {
        var reader = new XmlWorkspaceReader();
        Assert.Throws<DirectoryNotFoundException>(() => reader.Load("/nonexistent/path"));
    }
}
