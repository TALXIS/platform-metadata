using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtAuthoringTests
{
    [Fact]
    public void AddEntity_DeclaresPrimaryKeyField()
    {
        var schema = new CmtDataSchema();

        var account = schema.AddEntity("account", "Account", "accountid", "name");

        Assert.Same(account, schema.FindEntity("account"));
        var key = Assert.Single(account.Fields);
        Assert.Equal("accountid", key.Name);
        Assert.Equal(CmtFieldTypes.Guid, key.Type);
        Assert.True(key.IsPrimaryKey);
        Assert.Equal("Account", key.DisplayName);
        Assert.Equal("accountid", account.PrimaryIdField);
        Assert.Equal("name", account.PrimaryNameField);
        Assert.Empty(schema.EntityImportOrder);
    }

    [Fact]
    public void AddEntity_AppendsToImportOrderOnlyWhenPresent()
    {
        var schema = new CmtDataSchema();
        schema.AddEntity("account", "Account", "accountid", "name");
        schema.EntityImportOrder.Add("account");

        schema.AddEntity("contact", "Contact", "contactid", "fullname");

        Assert.Equal(new[] { "account", "contact" }, schema.EntityImportOrder);
    }

    [Fact]
    public void AddEntity_RejectsDuplicateNameOrdinally()
    {
        var schema = new CmtDataSchema();
        schema.AddEntity("account", "Account", "accountid", "name");

        Assert.Throws<InvalidOperationException>(() => schema.AddEntity("account", "Account", "accountid", "name"));
        schema.AddEntity("Account", "Account", "accountid", "name"); // a different name to CMT
    }

    [Fact]
    public void AddField_ReturnsFieldAndRejectsDuplicates()
    {
        var entity = new CmtDataSchema().AddEntity("account", "Account", "accountid", "name");

        var name = entity.AddField("name", CmtFieldTypes.String, "Account Name", updateCompare: true);
        var parent = entity.AddField("parentaccountid", CmtFieldTypes.EntityReference, lookupType: "account");

        Assert.Same(name, entity.FindField("name"));
        Assert.True(name.IsUpdateCompare);
        Assert.Equal("account", parent.LookupType);
        Assert.Throws<InvalidOperationException>(() => entity.AddField("name", CmtFieldTypes.String));
        Assert.Empty(new CmtDataSchemaValidator().Validate(entity.Fields.Count > 0 ? Schema(entity) : throw new InvalidOperationException()).Where(r => r.Severity == ValidationSeverity.Error));
    }

    private static CmtDataSchema Schema(CmtSchemaEntity entity)
    {
        var schema = new CmtDataSchema();
        schema.Entities.Add(entity);
        return schema;
    }

    [Fact]
    public void AddRelationship_RejectsDuplicateName()
    {
        var entity = new CmtDataSchema().AddEntity("account", "Account", "accountid", "name");
        entity.AddRelationship(new CmtSchemaRelationship { Name = "account_contact", IsManyToMany = true });

        Assert.Throws<InvalidOperationException>(() => entity.AddRelationship(new CmtSchemaRelationship { Name = "account_contact" }));
    }

    [Fact]
    public void DataHelpers_AddEntityRecordAndSetFields()
    {
        var data = new CmtData();
        var id = new Guid("11111111-1111-1111-1111-111111111111");

        var record = data.AddEntity("account", "Account").AddRecord(id)
            .Set("name", "Contoso")
            .Set("primarycontactid", "22222222-2222-2222-2222-222222222222", "contact", "Jane Doe")
            .Set("name", "Contoso Ltd");

        Assert.Same(record, data.FindEntity("account")!.Records.Single());
        Assert.Equal(2, record.Fields.Count);
        Assert.Equal("Contoso Ltd", record.Fields[0].Value);
        Assert.Equal("contact", record.Fields[1].LookupEntity);
        Assert.Throws<InvalidOperationException>(() => data.AddEntity("account"));
        Assert.Throws<InvalidOperationException>(() => data.FindEntity("account")!.AddRecord(id));
    }

    [Fact]
    public void DirectoryOverloads_RoundtripAndReportChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cmt-dir-{Guid.NewGuid():N}");
        try
        {
            var schema = new CmtDataSchema();
            var account = schema.AddEntity("account", "Account", "accountid", "name");
            account.AddField("name", CmtFieldTypes.String, "Account Name", updateCompare: true);
            var data = new CmtData();
            data.AddEntity("account", "Account").AddRecord(new Guid("11111111-1111-1111-1111-111111111111")).Set("name", "Contoso");

            var writer = new CmtPackageXmlWriter();
            Assert.True(writer.Save(new CmtPackage(schema, data), directory));
            Assert.True(File.Exists(Path.Combine(directory, CmtPackageLayout.SchemaFileName)));
            Assert.True(File.Exists(Path.Combine(directory, CmtPackageLayout.DataFileName)));

            var loaded = new CmtPackageXmlReader().LoadDirectory(directory);
            Assert.Empty(loaded.LoadErrors);
            Assert.Equal("Contoso", loaded.Data!.FindEntity("account")!.Records.Single().Fields.Single().Value);
            Assert.False(writer.Save(loaded, directory));

            loaded.Schema.FindEntity("account")!.AddField("telephone1", CmtFieldTypes.String, "Phone");
            Assert.True(writer.Save(loaded, directory));
            Assert.NotNull(new CmtPackageXmlReader().LoadDirectory(directory).Schema.FindEntity("account")!.FindField("telephone1"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LoadDirectory_WithoutDataFileHasNullDataAndNoError()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cmt-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, CmtPackageLayout.SchemaFileName), "<entities><entity name=\"account\"><fields /></entity></entities>");

            var package = new CmtPackageXmlReader().LoadDirectory(directory);

            Assert.Empty(package.LoadErrors);
            Assert.Null(package.Data);
            Assert.Single(package.Schema.Entities);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
