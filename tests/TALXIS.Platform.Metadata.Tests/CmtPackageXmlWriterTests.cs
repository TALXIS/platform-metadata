using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtPackageXmlWriterTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage");

    [Theory]
    [InlineData("basic")]
    [InlineData("many-to-many")]
    [InlineData("real-export")]
    [InlineData("talxis-dialect")]
    [InlineData("live-export")]
    public void LoadThenSaveIsByteIdentical(string fixture)
    {
        var schemaPath = Path.Combine(FixtureRoot, fixture, "data_schema.xml");
        var dataPath = Path.Combine(FixtureRoot, fixture, "data.xml");
        var package = new CmtPackageXmlReader().Load(schemaPath, dataPath);
        Assert.Empty(package.LoadErrors);

        var writer = new CmtPackageXmlWriter();
        Assert.Equal(File.ReadAllBytes(schemaPath), SaveOverCopy(schemaPath, path => writer.SaveSchema(package, path)));
        Assert.Equal(File.ReadAllBytes(dataPath), SaveOverCopy(dataPath, path => writer.SaveData(package, path)));
    }

    [Fact]
    public void SaveIfChangedLeavesUnchangedFileUntouched()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmt-untouched-{Guid.NewGuid():N}.xml");
        File.Copy(BasicSchemaPath, path);
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);
        try
        {
            var package = new CmtPackageXmlReader().Load(path, null);
            new CmtPackageXmlWriter().SaveSchema(package, path);

            Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
            Assert.Equal(File.ReadAllBytes(BasicSchemaPath), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RealExportMutationKeepsBomCrlfDeclarationAndEntitisedNewlines()
    {
        var schemaPath = Path.Combine(FixtureRoot, "real-export", "data_schema.xml");
        var dataPath = Path.Combine(FixtureRoot, "real-export", "data.xml");
        var package = new CmtPackageXmlReader().Load(schemaPath, dataPath);
        package.Schema.FindEntity("talxis_counterconfiguration")!.FindField("talxis_fieldname")!.IsUpdateCompare = true;
        package.Data!.FindEntity("talxis_counterconfiguration")!.Records[1].Fields.Single(f => f.Name == "talxis_fieldname").Value = "talxis_number";

        var writer = new CmtPackageXmlWriter();
        var schemaBytes = SaveOverCopy(schemaPath, path => writer.SaveSchema(package, path));
        var dataBytes = SaveOverCopy(dataPath, path => writer.SaveData(package, path));

        var expectedSchema = File.ReadAllText(schemaPath).Replace(
            "<field displayname=\"Field Name\" name=\"talxis_fieldname\" type=\"string\" customfield=\"true\" />",
            "<field displayname=\"Field Name\" name=\"talxis_fieldname\" type=\"string\" customfield=\"true\" updateCompare=\"true\" />");
        Assert.Equal(expectedSchema, System.Text.Encoding.UTF8.GetString(schemaBytes));
        Assert.Contains("\r\n", expectedSchema);

        var originalData = File.ReadAllBytes(dataPath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, dataBytes.Take(3));
        var expectedData = System.Text.Encoding.UTF8.GetString(originalData, 3, originalData.Length - 3)
            .Replace("<field name=\"talxis_fieldname\" value=\"talxis_internalid\" />\r\n        <field name=\"talxis_seriesname\" value=\"Autonumber series for &amp;#39;talxis_opportunityheader",
                     "<field name=\"talxis_fieldname\" value=\"talxis_number\" />\r\n        <field name=\"talxis_seriesname\" value=\"Autonumber series for &amp;#39;talxis_opportunityheader");
        var actualData = System.Text.Encoding.UTF8.GetString(dataBytes, 3, dataBytes.Length - 3);
        Assert.Equal(expectedData, actualData);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n", actualData);
        Assert.Contains("&#xD;&#xA;", actualData);
    }

    [Fact]
    public void TalxisDeclarationWithoutEncodingSurvivesRewrite()
    {
        var schemaPath = Path.Combine(FixtureRoot, "talxis-dialect", "data_schema.xml");
        var package = new CmtPackageXmlReader().Load(schemaPath, null);
        package.Schema.FindEntity("account")!.GuidSwap = false;

        var text = System.Text.Encoding.UTF8.GetString(SaveOverCopy(schemaPath, path => new CmtPackageXmlWriter().SaveSchema(package, path)));

        // Root attributes split over two lines cannot be preserved by XDocument; the declaration and body can.
        Assert.StartsWith("<?xml version=\"1.0\"?>\n<entities xmlns:xsi=", text);
        Assert.Contains("renderliquid=\"false\" guidswap=\"false\">", text);
        Assert.Contains("<!-- <field displayname=\"#\" name=\"logoid\"", text);
    }

    [Fact]
    public void AddedRecordIsInsertedAfterLastRecord()
    {
        var (package, original) = LoadManyToManyData();
        var tag = package.Data!.FindEntity("new_tag")!;
        tag.Records.Add(new CmtDataRecord { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000003"), Fields = { new CmtDataField { Name = "new_name", Value = "Gamma" } } });

        var anchor = "<field name=\"new_name\" value=\"Beta\" />" + NewLine(original) + "      </record>";
        var expected = original.Replace(anchor, anchor + NewLine(original) +
            "      <record id=\"bbbbbbbb-0000-0000-0000-000000000003\">" + NewLine(original) +
            "        <field name=\"new_name\" value=\"Gamma\" />" + NewLine(original) +
            "      </record>");
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveDataToText(package));
    }

    [Fact]
    public void RemovedRecordIsDeletedWithItsLines()
    {
        var (package, original) = LoadManyToManyData();
        var tag = package.Data!.FindEntity("new_tag")!;
        tag.Records.Remove(tag.Records.Single(r => r.Id == new Guid("bbbbbbbb-0000-0000-0000-000000000002")));

        var nl = NewLine(original);
        var expected = original.Replace(nl +
            "      <record id=\"bbbbbbbb-0000-0000-0000-000000000002\">" + nl +
            "        <field name=\"new_tagid\" value=\"bbbbbbbb-0000-0000-0000-000000000002\" />" + nl +
            "        <field name=\"new_name\" value=\"Beta\" />" + nl +
            "      </record>", string.Empty);
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveDataToText(package));
    }

    [Fact]
    public void ChangedTargetIdsRewriteOnlyTheTargetList()
    {
        var (package, original) = LoadManyToManyData();
        var m2m = package.Data!.FindEntity("new_project")!.ManyToManyRelationships.Single();
        m2m.TargetIds.RemoveAt(1);
        m2m.TargetIds.Add(new Guid("bbbbbbbb-0000-0000-0000-000000000003"));

        var expected = original.Replace("<targetid>bbbbbbbb-0000-0000-0000-000000000002</targetid>", "<targetid>bbbbbbbb-0000-0000-0000-000000000003</targetid>");
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveDataToText(package));
    }

    [Fact]
    public void PartiesAreAddedAndRemovedAsActivityPointerRecordsElements()
    {
        var dataPath = Path.Combine(FixtureRoot, "live-export", "data.xml");
        var package = new CmtPackageXmlReader().Load(Path.Combine(FixtureRoot, "live-export", "data_schema.xml"), dataPath);
        var appointment = package.Data!.FindEntity("appointment")!.Records.Single();
        var required = appointment.Fields.Single(f => f.Name == "requiredattendees").ActivityPointerRecords;
        required.RemoveAt(1);
        var optional = appointment.Fields.Single(f => f.Name == "optionalattendees").ActivityPointerRecords;
        optional.Add(new CmtDataRecord().Set("partyid", "ccdd83c3-e1c0-f111-a05a-6045bd091279", "contact", "CMTLAB CMTLAB Contact").Set("participationtypemask", "6"));

        var text = System.Text.Encoding.UTF8.GetString(SaveOverCopy(dataPath, path => new CmtPackageXmlWriter().SaveData(package, path)));

        Assert.Contains("<activitypointerrecords id=\"643b17ee-e3c0-f111-a05a-6045bd091279\">", text);
        Assert.DoesNotContain("653b17ee-e3c0-f111-a05a-6045bd091279", text);
        Assert.Contains(
            "<field name=\"optionalattendees\" value=\"\">\n" +
            "          <activitypointerrecords>\n" +
            "            <field name=\"partyid\" value=\"ccdd83c3-e1c0-f111-a05a-6045bd091279\" lookupentity=\"contact\" lookupentityname=\"CMTLAB CMTLAB Contact\" />\n" +
            "            <field name=\"participationtypemask\" value=\"6\" />\n" +
            "          </activitypointerrecords>\n" +
            "        </field>", text);

        var reread = new CmtPackageXmlReader().ReadData(System.Xml.Linq.XDocument.Parse(text));
        var parties = reread.FindEntity("appointment")!.Records.Single().Fields.Single(f => f.Name == "optionalattendees").ActivityPointerRecords;
        Assert.Equal(Guid.Empty, Assert.Single(parties).Id);
    }

    private static readonly string ManyToManyDataPath = Path.Combine(FixtureRoot, "many-to-many", "data.xml");

    private static (CmtPackage Package, string Original) LoadManyToManyData() =>
        (new CmtPackageXmlReader().Load(Path.Combine(FixtureRoot, "many-to-many", "data_schema.xml"), ManyToManyDataPath), File.ReadAllText(ManyToManyDataPath));

    private static string SaveDataToText(CmtPackage package) =>
        System.Text.Encoding.UTF8.GetString(SaveOverCopy(ManyToManyDataPath, path => new CmtPackageXmlWriter().SaveData(package, path)));

    [Fact]
    public void ToggleUpdateCompareChangesOnlyThatAttribute()
    {
        // A newly added attribute is appended after the existing ones; CMT itself writes updateCompare first,
        // but attribute order is irrelevant to both importers and existing attributes keep their position.
        var (package, original) = LoadBasicSchema();
        var schema = package.Schema;
        schema.FindEntity("account")!.FindField("name")!.IsUpdateCompare = true;

        var expected = original.Replace(
            "<field displayname=\"Account Name\" name=\"name\" type=\"string\" />",
            "<field displayname=\"Account Name\" name=\"name\" type=\"string\" updateCompare=\"true\" />");
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveSchemaToText(package));
    }

    [Fact]
    public void AddedFieldIsInsertedWithIndentation()
    {
        var (package, original) = LoadBasicSchema();
        var schema = package.Schema;
        schema.FindEntity("contact")!.Fields.Add(new CmtSchemaField { Name = "emailaddress1", DisplayName = "Email", Type = CmtFieldTypes.String });

        var anchor = "<field displayname=\"Company Name\" name=\"parentcustomerid\" type=\"customer\" lookupType=\"account|contact\" />";
        var expected = original.Replace(anchor, anchor + NewLine(original) + "      <field displayname=\"Email\" name=\"emailaddress1\" type=\"string\" />");
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveSchemaToText(package));
    }

    [Fact]
    public void RemovedFieldIsDeletedWithItsLine()
    {
        var (package, original) = LoadBasicSchema();
        var schema = package.Schema;
        var account = schema.FindEntity("account")!;
        account.Fields.Remove(account.FindField("creditlimit")!);

        var expected = original.Replace(NewLine(original) + "      <field displayname=\"Credit Limit\" name=\"creditlimit\" type=\"money\" />", string.Empty);
        Assert.NotEqual(original, expected);
        Assert.Equal(expected, SaveSchemaToText(package));
    }

    [Fact]
    public void EntityDifferingOnlyByCaseIsAddedNotMerged()
    {
        var (package, original) = LoadBasicSchema();
        package.Schema.Entities.Add(new CmtSchemaEntity { Name = "Account", PrimaryIdField = "accountid" });

        var text = SaveSchemaToText(package);

        // Ordinal keys: the existing <entity name="account"> is untouched and a new element is appended.
        Assert.Contains("<entity name=\"account\" displayname=\"Account\" etc=\"1\"", text);
        Assert.Contains("<entity name=\"Account\" primaryidfield=\"accountid\">", text);
    }

    [Fact]
    public void NullableFlagsRemoveOrKeepAttributes()
    {
        var (package, original) = LoadBasicSchema();
        var project = package.Schema.FindEntity("new_project")!;
        Assert.False(project.SkipUpdate);
        project.SkipUpdate = null;          // remove skipupdate="false"
        project.ForceCreate = false;        // write an explicit false
        package.Schema.FindEntity("account")!.RenderLiquid = true;

        var text = SaveSchemaToText(package);

        Assert.DoesNotContain("skipupdate", text);
        Assert.Contains("disableplugins=\"true\" forcecreate=\"false\">", text);
        Assert.Contains("disableplugins=\"false\" renderliquid=\"true\">", text);
        Assert.NotEqual(original, text);
    }

    [Fact]
    public void InMemoryModelSavesAndReadsBackEqual()
    {
        var schema = new CmtDataSchema { EntityImportOrder = { "new_tag", "new_project" } };
        schema.Entities.Add(new CmtSchemaEntity
        {
            Name = "new_project",
            DisplayName = "Project",
            ObjectTypeCode = 10001,
            PrimaryIdField = "new_projectid",
            PrimaryNameField = "new_name",
            DisablePlugins = false,
            SkipUpdate = true,
            RenderLiquid = true,
            GuidSwap = false,
            FetchXmlFilter = "<fetch><entity name=\"new_project\" /></fetch>",
            Fields =
            {
                new CmtSchemaField { Name = "new_projectid", DisplayName = "Project", Type = CmtFieldTypes.Guid, IsPrimaryKey = true, IsUpdateCompare = true },
                new CmtSchemaField { Name = "new_accountid", DisplayName = "Account", Type = CmtFieldTypes.EntityReference, LookupType = "account", IsCustomField = true }
            },
            Relationships =
            {
                new CmtSchemaRelationship { Name = "new_project_new_tag", IsManyToMany = true, IsReflexive = false, RelatedEntityName = "new_project_new_tag", M2mTargetEntity = "new_tag", M2mTargetEntityPrimaryKey = "new_tagid" }
            }
        });

        var data = new CmtData { Timestamp = "2026-01-15T10:00:00.0000000Z" };
        var projectId = Guid.NewGuid();
        data.Entities.Add(new CmtDataEntity
        {
            Name = "new_project",
            DisplayName = "Project",
            Records =
            {
                new CmtDataRecord
                {
                    Id = projectId,
                    NewId = Guid.NewGuid(),
                    Fields =
                    {
                        new CmtDataField { Name = "new_accountid", Value = Guid.Empty.ToString(), LookupEntity = "account", LookupEntityName = "Contoso" },
                        new CmtDataField { Name = "new_document", Value = "doc1", FileName = "contract.pdf" },
                        new CmtDataField
                        {
                            Name = "new_parties",
                            ActivityPointerRecords = { new CmtDataRecord { Id = Guid.NewGuid(), Fields = { new CmtDataField { Name = "partyid", Value = Guid.Empty.ToString(), LookupEntity = "contact" } } } }
                        }
                    }
                }
            },
            ManyToManyRelationships =
            {
                new CmtDataManyToManyRelationship { SourceId = projectId, TargetEntityName = "new_tag", TargetEntityNameIdField = "new_tagid", RelationshipName = "new_project_new_tag", RelationshipSchemaName = "new_project_new_tag", TargetIds = { Guid.NewGuid(), Guid.NewGuid() } }
            }
        });

        var directory = Path.Combine(Path.GetTempPath(), $"cmt-inmemory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var schemaPath = Path.Combine(directory, "data_schema.xml");
            var dataPath = Path.Combine(directory, "data.xml");
            var writer = new CmtPackageXmlWriter();
            var inMemory = new CmtPackage(schema, data);
            writer.SaveSchema(inMemory, schemaPath);
            writer.SaveData(inMemory, dataPath);

            var package = new CmtPackageXmlReader().Load(schemaPath, dataPath);

            Assert.Empty(package.LoadErrors);
            Assert.Equal(ToJson(schema), ToJson(package.Schema));
            Assert.Equal(ToJson(data), ToJson(package.Data!));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static readonly string BasicSchemaPath = Path.Combine(FixtureRoot, "basic", "data_schema.xml");

    private static (CmtPackage Package, string Original) LoadBasicSchema() =>
        (new CmtPackageXmlReader().Load(BasicSchemaPath, null), File.ReadAllText(BasicSchemaPath));

    private static string SaveSchemaToText(CmtPackage package) =>
        System.Text.Encoding.UTF8.GetString(SaveOverCopy(BasicSchemaPath, path => new CmtPackageXmlWriter().SaveSchema(package, path)));

    private static string NewLine(string text) => text.Contains("\r\n") ? "\r\n" : "\n";

    private static byte[] SaveOverCopy(string originalPath, Action<string> save)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmt-{Guid.NewGuid():N}.xml");
        File.Copy(originalPath, path);
        try
        {
            save(path);
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
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
