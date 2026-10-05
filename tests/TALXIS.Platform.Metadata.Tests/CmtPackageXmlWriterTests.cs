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
    public void ToggleUpdateCompareChangesOnlyThatAttribute()
    {
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
        (new CmtPackageXmlReader().Load(BasicSchemaPath), File.ReadAllText(BasicSchemaPath));

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
