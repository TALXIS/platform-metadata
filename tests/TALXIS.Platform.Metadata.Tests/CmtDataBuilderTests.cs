using TALXIS.Platform.Metadata.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtDataBuilderTests
{
    private static readonly Guid TeamId = new("e3568308-b2b6-ed11-83fe-6045bda1c26e");

    private static CmtDataEntity TeamsFromMain()
    {
        var entity = new CmtDataEntity { Name = "talxis_securityteam", DisplayName = "Security Team" };
        entity.AddRecord(TeamId).Set("talxis_securityteamid", TeamId.ToString()).Set("talxis_name", "Sales").Set("talxis_code", "S").Set("talxis_isdefault", "False");
        return entity;
    }

    private static CmtDataEntity TeamsFromSecurityRules()
    {
        var entity = new CmtDataEntity { Name = "talxis_securityteam" };
        entity.AddRecord(TeamId).Set("talxis_securityteamid", TeamId.ToString()).Set("talxis_name", "Sales");
        entity.ManyToManyRelationships.Add(new CmtDataManyToManyRelationship
        {
            SourceId = TeamId,
            TargetEntityName = "talxis_product",
            RelationshipName = "ntg_talxis_product_talxis_securityteam",
            TargetIds = { new Guid("d5d28c4c-3d94-ee11-be37-000d3a44810c") },
        });
        return entity;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MergeEntity_KeepsTheFullerRecordWhicheverPackageComesFirst(bool mainFirst)
    {
        var data = new CmtData();
        var packages = mainFirst ? new[] { TeamsFromMain(), TeamsFromSecurityRules() } : new[] { TeamsFromSecurityRules(), TeamsFromMain() };
        var warnings = new List<string>();

        foreach (var entity in packages) CmtDataBuilder.MergeEntity(data, entity, warnings);

        var team = Assert.Single(Assert.Single(data.Entities).Records);
        Assert.Equal(new[] { "talxis_securityteamid", "talxis_name", "talxis_code", "talxis_isdefault" }.OrderBy(n => n), team.Fields.Select(f => f.Name).OrderBy(n => n));
        Assert.Single(data.Entities[0].ManyToManyRelationships);
        Assert.Equal("Security Team", data.Entities[0].DisplayName);
        Assert.Empty(warnings);
    }

    [Fact]
    public void MergeEntity_KeepsFirstValueAndReportsConflictOncePerField()
    {
        var data = new CmtData();
        var warnings = new List<string>();
        var other = TeamsFromSecurityRules();
        other.Records[0].Set("talxis_name", "Sales EU");

        CmtDataBuilder.MergeEntity(data, TeamsFromMain(), warnings);
        CmtDataBuilder.MergeEntity(data, other, warnings);

        Assert.Equal("Sales", data.Entities[0].Records[0].Fields.Single(f => f.Name == "talxis_name").Value);
        var warning = Assert.Single(warnings);
        Assert.Contains("'talxis_name'", warning);
        Assert.Contains("1 record(s)", warning);
    }

    [Fact]
    public void MergeEntity_MergesRecordRepeatedInsideOnePackage()
    {
        var entity = new CmtDataEntity { Name = "talxis_wastecollectionbintype" };
        var id = Guid.NewGuid();
        entity.Records.Add(new CmtDataRecord { Id = id, Fields = { new CmtDataField { Name = "talxis_name", Value = "Bin" } } });
        entity.Records.Add(new CmtDataRecord { Id = id, Fields = { new CmtDataField { Name = "ntg_sell", Value = "True" } } });
        var data = new CmtData();

        CmtDataBuilder.MergeEntity(data, entity);

        var record = Assert.Single(data.Entities[0].Records);
        Assert.Equal(new[] { "talxis_name", "ntg_sell" }, record.Fields.Select(f => f.Name));
    }

    [Fact]
    public void MergeEntity_UnitesManyToManyTargetIds()
    {
        var data = new CmtData();
        var first = TeamsFromSecurityRules();
        var second = TeamsFromSecurityRules();
        var extra = Guid.NewGuid();
        second.ManyToManyRelationships[0].TargetIds.Add(extra);

        CmtDataBuilder.MergeEntity(data, first);
        CmtDataBuilder.MergeEntity(data, second);

        var association = Assert.Single(data.Entities[0].ManyToManyRelationships);
        Assert.Equal(2, association.TargetIds.Count);
        Assert.Contains(extra, association.TargetIds);
    }
}
