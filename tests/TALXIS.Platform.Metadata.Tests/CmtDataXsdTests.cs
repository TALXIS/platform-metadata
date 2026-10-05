using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtDataXsdTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage");
    private readonly SchemaValidator _validator = new();

    [Theory]
    [InlineData("basic", "data_schema.xml")]
    [InlineData("basic", "data.xml")]
    [InlineData("many-to-many", "data_schema.xml")]
    [InlineData("many-to-many", "data.xml")]
    [InlineData("real-export", "data_schema.xml")]
    [InlineData("real-export", "data.xml")]
    [InlineData("talxis-dialect", "data_schema.xml")]
    [InlineData("talxis-dialect", "data.xml")]
    public void FixturesPassXsd(string fixture, string file)
    {
        Assert.Empty(_validator.ValidateFile(Path.Combine(FixtureRoot, fixture, file)));
    }

    [Fact]
    public void PackageWrittenFromScratchPassesXsd()
    {
        var schema = new CmtDataSchema { DateMode = "absolute" };
        schema.Entities.Add(new CmtSchemaEntity
        {
            Name = "contact",
            PrimaryIdField = "contactid",
            PrimaryNameField = "fullname",
            ForceCreate = true,
            FetchXmlFilter = "<fetch><entity name=\"contact\" /></fetch>",
            Fields =
            {
                new CmtSchemaField { Name = "contactid", Type = CmtFieldTypes.Guid, IsPrimaryKey = true },
                new CmtSchemaField { Name = "fullname", Type = CmtFieldTypes.String, IsUpdateCompare = true },
                new CmtSchemaField { Name = "birthdate", Type = CmtFieldTypes.DateTime, DateMode = "absolute" },
                new CmtSchemaField { Name = "parentcustomerid", Type = CmtFieldTypes.EntityReference, LookupType = "account" },
            },
            Relationships =
            {
                new CmtSchemaRelationship
                {
                    Name = "contact_customer_accounts",
                    ReferencingEntity = "contact",
                    ReferencingAttribute = "parentcustomerid",
                    ReferencedEntity = "account",
                    ReferencedAttribute = "accountid",
                },
            },
        });
        schema.EntityImportOrder.Add("contact");
        var data = new CmtData();
        data.Entities.Add(new CmtDataEntity
        {
            Name = "contact",
            Records = { new CmtDataRecord { Id = Guid.NewGuid(), Fields = { new CmtDataField { Name = "fullname", Value = "Jane" } } } },
        });

        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var package = new CmtPackage(schema, data);
            var writer = new CmtPackageXmlWriter();
            writer.SaveSchema(package, Path.Combine(folder, "data_schema.xml"));
            writer.SaveData(package, Path.Combine(folder, "data.xml"));

            Assert.Empty(_validator.ValidateFile(Path.Combine(folder, "data_schema.xml")));
            Assert.Empty(_validator.ValidateFile(Path.Combine(folder, "data.xml")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void UnknownElementIsStillRejected()
    {
        var doc = XDocument.Parse("<entities><entity name=\"a\"><fields /><bogus /></entity></entities>");

        Assert.Contains(_validator.ValidateXml(doc, "data_schema.xml"), r => r.Severity == ValidationSeverity.Error);
    }

    [Theory]
    [InlineData("""<entities><entity name="email"><records><record id="11111111-1111-1111-1111-111111111111" newId="22222222-2222-2222-2222-222222222222"><field name="to"><activitypointerrecords><activitypointerrecord id="33333333-3333-3333-3333-333333333333"><field name="partyid" value="44444444-4444-4444-4444-444444444444" lookupentity="contact" /></activitypointerrecord></activitypointerrecords></field></record></records></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><records><record id="11111111-1111-1111-1111-111111111111"><field name="doc" value="f1" filename="contract.pdf" /></record></records><relationships><relationship name="a_b" manyToMany="true" /></relationships></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><records /><m2mrelationships><m2mrelationship sourceid="11111111-1111-1111-1111-111111111111" targetentityname="b" m2mrelationshipname="a_b" m2mrelationshipschemaname="a_b"><targetids /></m2mrelationship></m2mrelationships></entity></entities>""")]
    [InlineData("""<entities dateMode="relativeDaily"><entity name="a"><fields><field name="aid" type="guid" /><field name="d" type="datetime" dateMode="relative" /></fields><relationships /><filter>x</filter></entity></entities>""")]
    public void RealCmtShapesPassXsd(string xml)
    {
        Assert.Empty(_validator.ValidateXml(XDocument.Parse(xml), "data.xml").Where(r => r.Severity == ValidationSeverity.Error));
    }

    [Theory]
    [InlineData("""<entities><entity name="a"><fields /><filter>x</filter><relationships /></entity></entities>""")]
    [InlineData("""<entities dateMode="Absolute"><entity name="a"><fields /></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><fields><field name="d" type="datetime" dateMode="daily" /></fields></entity></entities>""")]
    public void WrongOrderOrDateModeIsRejected(string xml)
    {
        Assert.Contains(_validator.ValidateXml(XDocument.Parse(xml), "data_schema.xml"), r => r.Severity == ValidationSeverity.Error);
    }
}
