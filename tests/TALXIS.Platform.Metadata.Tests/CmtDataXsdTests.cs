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
}
