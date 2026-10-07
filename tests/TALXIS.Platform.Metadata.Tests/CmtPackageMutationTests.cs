using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtPackageMutationTests : IDisposable
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", "exhaustive");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cmt-mutation-{Guid.NewGuid():N}");

    public CmtPackageMutationTests()
    {
        Directory.CreateDirectory(_root);
        foreach (var file in new[] { CmtPackageLayout.SchemaFileName, CmtPackageLayout.DataFileName })
            File.Copy(Path.Combine(FixturePath, file), Path.Combine(_root, file));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    // The writer once compared against the in-memory document, which the first save had already patched: the second
    // target got the stale source file and a save back to the loaded path was skipped.
    [Fact]
    public void EditSavedTwice_BothFilesContainEdit()
    {
        var schemaPath = Path.Combine(_root, CmtPackageLayout.SchemaFileName);
        var copyPath = Path.Combine(_root, "copy.xml");
        var package = new CmtPackageXmlReader().Load(schemaPath);
        package.Schema.FindEntity("account")!.FindField("telephone1")!.IsUpdateCompare = true;
        var writer = new CmtPackageXmlWriter();

        writer.SaveSchema(package, copyPath);
        writer.SaveSchema(package, schemaPath);

        Assert.Contains("name=\"telephone1\" type=\"string\" updateCompare=\"true\"", File.ReadAllText(copyPath));
        Assert.Contains("name=\"telephone1\" type=\"string\" updateCompare=\"true\"", File.ReadAllText(schemaPath));
    }

    [Fact]
    public void Edits_PatchOnlyTheirLines()
    {
        var schemaText = File.ReadAllText(Path.Combine(_root, CmtPackageLayout.SchemaFileName));
        var dataText = File.ReadAllText(Path.Combine(_root, CmtPackageLayout.DataFileName));
        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        var parent = package.Schema.FindEntity("cmtl_parent")!;
        parent.RenderLiquid = null;
        parent.DisablePlugins = true;
        parent.AddField("cmtl_code", CmtFieldTypes.String, "Code");
        package.Schema.FindEntity("account")!.FetchXmlFilter = null;

        var data = package.Data!;
        var parents = data.FindEntity("cmtl_parent")!;
        parents.Records.RemoveAt(1);
        parents.ManyToManyRelationships[0].TargetIds.RemoveAt(1);
        data.FindEntity("account")!.AddRecord(new Guid("5f1c2a40-7b3e-4c6d-9e8f-0a1b2c3d4e5f")).Set("name", "Fabrikam");
        data.FindEntity("appointment")!.Records[0].Fields.Single(f => f.Name == "requiredattendees").ActivityPointerRecords.RemoveAt(1);

        Assert.True(new CmtPackageXmlWriter().Save(package, _root));

        var expectedSchema = RemoveLines(schemaText
                .Replace("<entity name=\"cmtl_parent\" renderliquid=\"false\" guidswap=\"false\" displayname=\"CMTL Parent\" primaryidfield=\"cmtl_parentid\" primarynamefield=\"cmtl_name\" disableplugins=\"false\">",
                         "<entity name=\"cmtl_parent\" guidswap=\"false\" displayname=\"CMTL Parent\" primaryidfield=\"cmtl_parentid\" primarynamefield=\"cmtl_name\" disableplugins=\"true\">")
                .Replace("<field displayname=\"Owner\" name=\"ownerid\" type=\"owner\" />\n    </fields>\n    <relationships>",
                         "<field displayname=\"Owner\" name=\"ownerid\" type=\"owner\" />\n      <field displayname=\"Code\" name=\"cmtl_code\" type=\"string\" />\n    </fields>\n    <relationships>"),
            "<filter>&lt;fetch&gt;&lt;entity name=\"account\"", "</filter>");
        Assert.Equal(expectedSchema, File.ReadAllText(Path.Combine(_root, CmtPackageLayout.SchemaFileName)));

        var expectedData = RemoveLines(RemoveLines(dataText, "<record id=\"3e05ec78-a3ac-514a-97d0-3a8db43dce8a\">", "</record>"),
                "<activitypointerrecords id=\"099a6847-d467-5185-8440-35933b1e9e75\">", "</activitypointerrecords>")
            .Replace("\n          <targetid>ad3c90ed-fb18-5c67-a628-25e89dd1c81a</targetid>", string.Empty)
            .Replace("</record>\n    </records>\n    <m2mrelationships />\n  </entity>\n  <entity name=\"contact\"",
                     "</record>\n      <record id=\"5f1c2a40-7b3e-4c6d-9e8f-0a1b2c3d4e5f\">\n        <field name=\"name\" value=\"Fabrikam\" />\n      </record>\n    </records>\n    <m2mrelationships />\n  </entity>\n  <entity name=\"contact\"");
        Assert.Equal(expectedData, File.ReadAllText(Path.Combine(_root, CmtPackageLayout.DataFileName)));
    }

    [Fact]
    public void AuthoredPackage_LoadsBackEqualAndValid()
    {
        var directory = Path.Combine(_root, "authored");
        var schema = new CmtDataSchema { DateMode = CmtDateModes.Absolute };
        schema.EntityImportOrder.Add("account");
        var account = schema.AddEntity("account", "accountid", "name", "Account");
        account.AddField("name", CmtFieldTypes.String, "Account Name", updateCompare: true);
        account.AddField("primarycontactid", CmtFieldTypes.EntityReference, "Primary Contact", lookupType: "contact");
        account.AddRelationship(new CmtSchemaRelationship { Name = "account_contact", IsManyToMany = true, M2mTargetEntity = "contact", M2mTargetEntityPrimaryKey = "contactid" });
        var contact = schema.AddEntity("contact", "contactid", "lastname");
        contact.AddField("lastname", CmtFieldTypes.String, updateCompare: true);
        var appointment = schema.AddEntity("appointment", "activityid", "subject");
        appointment.AddField("subject", CmtFieldTypes.String, updateCompare: true);
        appointment.AddField("requiredattendees", CmtFieldTypes.PartyList);

        var accountId = new Guid("11111111-1111-1111-1111-111111111111");
        var contactId = new Guid("22222222-2222-2222-2222-222222222222");
        var appointmentId = new Guid("33333333-3333-3333-3333-333333333333");
        var data = new CmtData { Timestamp = "2026-01-15T10:00:00.0000000Z" };
        var accounts = data.AddEntity("account", "Account");
        accounts.AddRecord(accountId).Set("accountid", accountId.ToString()).Set("name", "Contoso")
            .Set("primarycontactid", contactId.ToString(), "contact", "Smith");
        accounts.ManyToManyRelationships.Add(new CmtDataManyToManyRelationship
        {
            SourceId = accountId, TargetEntityName = "contact", TargetEntityNameIdField = "contactid", RelationshipName = "account_contact", TargetIds = { contactId }
        });
        data.AddEntity("contact").AddRecord(contactId).Set("contactid", contactId.ToString()).Set("lastname", "Smith");
        var meeting = data.AddEntity("appointment").AddRecord(appointmentId).Set("activityid", appointmentId.ToString()).Set("subject", "Kick-off").Set("requiredattendees", string.Empty);
        // CMT imports a party without an id; it reads back as Guid.Empty.
        meeting.Fields.Single(f => f.Name == "requiredattendees").ActivityPointerRecords.Add(
            new CmtDataRecord().Set("partyid", contactId.ToString(), "contact", "Smith").Set("participationtypemask", "5"));

        Assert.True(new CmtPackageXmlWriter().Save(new CmtPackage(schema, data), directory));
        var package = new CmtPackageXmlReader().LoadDirectory(directory);

        Assert.Empty(package.LoadErrors);
        Assert.Equal(ToJson(schema), ToJson(package.Schema));
        Assert.Equal(ToJson(data), ToJson(package.Data!));
        Assert.Empty(new SchemaValidator().ValidateFile(Path.Combine(directory, CmtPackageLayout.SchemaFileName)));
        Assert.Empty(new SchemaValidator().ValidateFile(Path.Combine(directory, CmtPackageLayout.DataFileName)));
        Assert.DoesNotContain(new CmtDataSchemaValidator().Validate(package.Schema), r => r.Severity == ValidationSeverity.Error);
        Assert.DoesNotContain(new CmtPackageValidator().Validate(package), r => r.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void AddHelpers_RejectDuplicatesOrdinally()
    {
        var schema = new CmtDataSchema();
        var account = schema.AddEntity("account", "accountid", "name");
        var data = new CmtData();
        var record = data.AddEntity("account").AddRecord(Guid.Empty);

        Assert.Throws<InvalidOperationException>(() => schema.AddEntity("account", "accountid", "name"));
        Assert.Throws<InvalidOperationException>(() => account.AddField("accountid", CmtFieldTypes.Guid));
        Assert.Throws<InvalidOperationException>(() => data.AddEntity("account"));
        Assert.Throws<InvalidOperationException>(() => data.FindEntity("account")!.AddRecord(Guid.Empty));
        Assert.Same(record, record.Set("name", "a").Set("name", "b"));
        Assert.Equal("b", Assert.Single(record.Fields).Value);
        // CMT compares names ordinally, so a name differing only by case is a different entity.
        schema.AddEntity("Account", "accountid", "name");
        Assert.Empty(schema.EntityImportOrder);
    }

    // Removes the lines from the one holding start through the one holding the next end marker.
    private static string RemoveLines(string text, string start, string end)
    {
        var from = text.LastIndexOf('\n', text.IndexOf(start, StringComparison.Ordinal));
        var to = text.IndexOf(end, from, StringComparison.Ordinal) + end.Length;
        return text.Remove(from, to - from);
    }

    private static string ToJson(object model)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            foreach (var property in info.Properties.Where(p => p.Name == nameof(MetadataBase.Source)).ToList()) info.Properties.Remove(property);
        });
        return JsonSerializer.Serialize(model, new JsonSerializerOptions { TypeInfoResolver = resolver });
    }
}
