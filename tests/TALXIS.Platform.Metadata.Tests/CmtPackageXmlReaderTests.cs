using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtPackageXmlReaderTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage");

    private static CmtPackage LoadFixture(string name) => new CmtPackageXmlReader().Load(
        Path.Combine(FixtureRoot, name, "data_schema.xml"),
        Path.Combine(FixtureRoot, name, "data.xml"));

    [Fact]
    public void ReadsSchemaEntitiesFieldsAndImportOrder()
    {
        var package = LoadFixture("basic");

        Assert.Empty(package.LoadErrors);
        Assert.Equal(new[] { "account", "contact", "new_project" }, package.Schema.Entities.Select(e => e.Name));
        Assert.Equal(new[] { "account", "contact", "new_project" }, package.Schema.EntityImportOrder);

        var account = package.Schema.FindEntity("ACCOUNT")!;
        Assert.Equal("Account", account.DisplayName);
        Assert.Equal(1, account.ObjectTypeCode);
        Assert.Equal("accountid", account.PrimaryIdField);
        Assert.Equal("name", account.PrimaryNameField);
        Assert.False(account.DisablePlugins);
        Assert.Equal(4, account.Fields.Count);

        var id = account.FindField("accountid")!;
        Assert.True(id.IsPrimaryKey);
        Assert.Equal(CmtFieldTypes.Guid, id.Type);

        var lookup = account.FindField("primarycontactid")!;
        Assert.Equal(CmtFieldTypes.EntityReference, lookup.Type);
        Assert.Equal("contact", lookup.LookupType);

        var project = package.Schema.FindEntity("new_project")!;
        Assert.True(project.DisablePlugins);
        Assert.False(project.SkipUpdate);
        Assert.True(project.FindField("new_projectid")!.IsUpdateCompare);
        Assert.True(project.FindField("new_name")!.IsCustomField);
        Assert.Equal("absolute", project.FindField("new_startdate")!.DateMode);
        Assert.StartsWith("<fetch>", project.FetchXmlFilter);
    }

    [Fact]
    public void ReadsDataRecordsAndLookups()
    {
        var data = LoadFixture("basic").Data!;

        Assert.Equal("2026-01-15T10:00:00.0000000Z", data.Timestamp);
        var project = data.FindEntity("new_project")!;
        Assert.Equal(2, project.Records.Count);

        var record = project.Records[0];
        Assert.Equal(new Guid("33333333-3333-3333-3333-333333333333"), record.Id);
        Assert.Equal("Sample Project & Co", record.Fields.Single(f => f.Name == "new_name").Value);

        var lookup = record.Fields.Single(f => f.Name == "new_accountid");
        Assert.Equal("account", lookup.LookupEntity);
        Assert.Equal("Contoso Ltd", lookup.LookupEntityName);
        Assert.Empty(project.ManyToManyRelationships);
    }

    [Fact]
    public void ReadsManyToManyRelationships()
    {
        var package = LoadFixture("many-to-many");

        var relationship = package.Schema.FindEntity("new_project")!.Relationships.Single();
        Assert.Equal("new_project_new_tag", relationship.Name);
        Assert.True(relationship.IsManyToMany);
        Assert.False(relationship.IsReflexive);
        Assert.Equal("new_tag", relationship.M2mTargetEntity);
        Assert.Equal("new_tagid", relationship.M2mTargetEntityPrimaryKey);

        var m2m = package.Data!.FindEntity("new_project")!.ManyToManyRelationships.Single();
        Assert.Equal(new Guid("aaaaaaaa-0000-0000-0000-000000000001"), m2m.SourceId);
        Assert.Equal("new_tag", m2m.TargetEntityName);
        Assert.Equal("new_tagid", m2m.TargetEntityNameIdField);
        Assert.Equal("new_project_new_tag", m2m.RelationshipName);
        Assert.Equal(2, m2m.TargetIds.Count);
    }

    [Fact]
    public void SetsSourceLocation()
    {
        var package = LoadFixture("basic");
        var schemaPath = Path.Combine(FixtureRoot, "basic", "data_schema.xml");

        var field = package.Schema.FindEntity("account")!.FindField("name")!;
        Assert.Equal(schemaPath, field.Source!.FilePath);
        Assert.Equal(5, field.Source.Line);
    }

    [Fact]
    public void MalformedXmlIsReportedAsLoadError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmt-malformed-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<entities>\n  <entity name=\"account\">\n</entities>");
        try
        {
            var package = new CmtPackageXmlReader().Load(path);

            var error = Assert.Single(package.LoadErrors);
            Assert.Equal(path, error.FilePath);
            Assert.NotNull(error.Line);
            Assert.Empty(package.Schema.Entities);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingDataFileIsReportedAsLoadError()
    {
        var package = new CmtPackageXmlReader().Load(
            Path.Combine(FixtureRoot, "basic", "data_schema.xml"),
            Path.Combine(FixtureRoot, "basic", "missing.xml"));

        Assert.Single(package.LoadErrors);
        Assert.Null(package.Data);
        Assert.Equal(3, package.Schema.Entities.Count);
    }
}
