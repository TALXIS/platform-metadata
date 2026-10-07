using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.ConfigurationMigration.Building;
using TALXIS.Platform.Metadata.ConfigurationMigration.Data;

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
    public void MergeEntity_ReportsConflictsMergesRepeatedRecordsAndUnitesTargets()
    {
        var data = new CmtData();
        var warnings = new List<string>();
        var other = TeamsFromSecurityRules();
        other.Records[0].Set("talxis_name", "Sales EU");
        var extra = Guid.NewGuid();
        other.ManyToManyRelationships[0].TargetIds.Add(extra);
        var repeated = new CmtDataEntity { Name = "talxis_wastecollectionbintype" };
        var id = Guid.NewGuid();
        repeated.Records.Add(new CmtDataRecord { Id = id, Fields = { new CmtDataField { Name = "talxis_name", Value = "Bin" } } });
        repeated.Records.Add(new CmtDataRecord { Id = id, Fields = { new CmtDataField { Name = "ntg_sell", Value = "True" } } });

        CmtDataBuilder.MergeEntity(data, TeamsFromSecurityRules(), warnings);
        CmtDataBuilder.MergeEntity(data, other, warnings);
        CmtDataBuilder.MergeEntity(data, repeated, warnings);

        var team = data.FindEntity("talxis_securityteam")!;
        Assert.Equal("Sales", team.Records[0].Fields.Single(f => f.Name == "talxis_name").Value);
        var warning = Assert.Single(warnings);
        Assert.Contains("'talxis_name'", warning);
        Assert.Contains("1 record(s)", warning);
        var association = Assert.Single(team.ManyToManyRelationships);
        Assert.Equal(2, association.TargetIds.Count);
        Assert.Contains(extra, association.TargetIds);
        var bin = Assert.Single(data.FindEntity("talxis_wastecollectionbintype")!.Records);
        Assert.Equal(new[] { "talxis_name", "ntg_sell" }, bin.Fields.Select(f => f.Name));
    }

    [Fact]
    public void MergeEntity_KeepsFirstTargetsWhenAnotherPackageLinksToADifferentTable()
    {
        var data = new CmtData();
        var warnings = new List<string>();
        var wrongTable = TeamsFromSecurityRules();
        wrongTable.ManyToManyRelationships[0].TargetEntityName = "account";
        wrongTable.ManyToManyRelationships[0].TargetIds.Clear();
        wrongTable.ManyToManyRelationships[0].TargetIds.Add(new Guid("aaaaaaaa-0000-0000-0000-000000000003"));

        CmtDataBuilder.MergeEntity(data, TeamsFromSecurityRules(), warnings);
        CmtDataBuilder.MergeEntity(data, wrongTable, warnings);

        var association = Assert.Single(data.FindEntity("talxis_securityteam")!.ManyToManyRelationships);
        Assert.Equal("talxis_product", association.TargetEntityName);
        Assert.Equal(new[] { new Guid("d5d28c4c-3d94-ee11-be37-000d3a44810c") }, association.TargetIds);
        Assert.Contains("to 'talxis_product' in one package and to 'account' in another", Assert.Single(warnings));
    }

    [Theory]
    [InlineData("attendees", true)]
    [InlineData("lookup name", true)]
    [InlineData("file name", true)]
    [InlineData("nothing", false)]
    public void MergeEntity_ComparesEverythingTheImporterReadsFromAField(string differsIn, bool expectConflict)
    {
        CmtDataEntity Appointment(bool later)
        {
            var entity = new CmtDataEntity { Name = "appointment" };
            var record = entity.AddRecord(new Guid("cccccccc-0000-0000-0000-000000000001"));
            var attendees = new CmtDataField { Name = "requiredattendees" };
            attendees.ActivityPointerRecords.Add(Attendee(new Guid("dddddddd-0000-0000-0000-000000000001")));
            if (later && differsIn == "attendees") attendees.ActivityPointerRecords.Add(Attendee(new Guid("dddddddd-0000-0000-0000-000000000002")));
            record.Fields.Add(attendees);
            record.Fields.Add(new CmtDataField { Name = "regardingobjectid", Value = "11111111-0000-0000-0000-000000000001", LookupEntity = "account", LookupEntityName = later && differsIn == "lookup name" ? "Fabrikam" : "Contoso" });
            record.Fields.Add(new CmtDataField { Name = "new_document", Value = "f1", FileName = later && differsIn == "file name" ? "offer-v2.pdf" : "offer.pdf" });
            return entity;
        }

        CmtDataRecord Attendee(Guid id)
        {
            var attendee = new CmtDataRecord { Id = id };
            attendee.Fields.Add(new CmtDataField { Name = "partyid", Value = id.ToString(), LookupEntity = "contact" });
            return attendee;
        }

        var data = new CmtData();
        var warnings = new List<string>();

        CmtDataBuilder.MergeEntity(data, Appointment(later: false), warnings);
        CmtDataBuilder.MergeEntity(data, Appointment(later: true), warnings);

        Assert.Equal(expectConflict ? 1 : 0, warnings.Count);
        Assert.Single(data.FindEntity("appointment")!.Records[0].Fields.Single(f => f.Name == "requiredattendees").ActivityPointerRecords);
    }

    [Theory]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001", "aaaaaaaa-0000-0000-0000-000000000002", true, "aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001", "aaaaaaaa-0000-0000-0000-000000000001", false, "aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData(null, "aaaaaaaa-0000-0000-0000-000000000002", false, "aaaaaaaa-0000-0000-0000-000000000002")]
    public void MergeEntity_ReportsCopiesThatAskForDifferentNewIds(string? firstNewId, string? laterNewId, bool expectWarning, string expectedNewId)
    {
        CmtDataEntity Teams(string? newId)
        {
            var entity = TeamsFromSecurityRules();
            entity.Records[0].NewId = newId == null ? null : new Guid(newId);
            return entity;
        }

        var data = new CmtData();
        var warnings = new List<string>();

        CmtDataBuilder.MergeEntity(data, Teams(firstNewId), warnings);
        CmtDataBuilder.MergeEntity(data, Teams(laterNewId), warnings);

        Assert.Equal(new Guid(expectedNewId), data.FindEntity("talxis_securityteam")!.Records[0].NewId);
        Assert.Equal(expectWarning, warnings.Any(w => w.Contains("different newId values in 1 record(s)")));
    }
}
