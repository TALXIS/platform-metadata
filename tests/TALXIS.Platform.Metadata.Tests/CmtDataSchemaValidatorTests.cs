using System.Xml.Linq;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtDataSchemaValidatorTests
{
    private static IReadOnlyList<ValidationResult> Validate(string xml) =>
        new CmtDataSchemaValidator().ValidateXml(XDocument.Parse(xml, LoadOptions.SetLineInfo), "data_schema.xml");

    [Fact]
    public void EntityWithUpdateCompareField_PassesValidation()
    {
        var results = Validate("""
            <entities>
              <entity name="talxis_configuration" displayname="Configuration">
                <fields>
                  <field updateCompare="true" name="talxis_configurationid" type="guid" primaryKey="true" />
                  <field name="talxis_value" type="string" customfield="true" />
                </fields>
              </entity>
            </entities>
            """);

        Assert.Empty(results);
    }

    [Fact]
    public void EntityWithoutUpdateCompareField_ReportsError()
    {
        var results = Validate("""
            <entities>
              <entity name="talxis_configuration" displayname="Configuration">
                <fields>
                  <field name="talxis_configurationid" type="guid" primaryKey="true" />
                  <field name="talxis_value" type="string" customfield="true" />
                </fields>
              </entity>
            </entities>
            """);

        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Equal(ValidationDiagnostics.CmtEntityMissingUpdateCompare, finding.Code);
        Assert.Contains("talxis_configuration", finding.Message);
        Assert.True(finding.Line > 0);
    }

    [Fact]
    public void MixedEntities_ReportsOnlyOffenders()
    {
        var results = Validate("""
            <entities>
              <entity name="talxis_good">
                <fields>
                  <field updateCompare="true" name="talxis_goodid" type="guid" primaryKey="true" />
                </fields>
              </entity>
              <entity name="talxis_bad">
                <fields>
                  <field name="talxis_badid" type="guid" primaryKey="true" />
                </fields>
              </entity>
            </entities>
            """);

        var finding = Assert.Single(results);
        Assert.Contains("talxis_bad", finding.Message);
    }

    [Fact]
    public void DataFormEntityWithRecords_IsSkipped()
    {
        var results = Validate("""
            <entities>
              <entity name="talxis_configuration">
                <records>
                  <record id="a1b2c3d4-0000-0000-0000-000000000001">
                    <field name="talxis_value" value="42" />
                  </record>
                </records>
              </entity>
            </entities>
            """);

        Assert.Empty(results);
    }

    [Fact]
    public void NonCmtDocument_IsSkipped()
    {
        var results = Validate("<ImportExportXml><Entities /></ImportExportXml>");

        Assert.Empty(results);
    }

    [Fact]
    public void MalformedXmlFile_IsSkippedNotDuplicated()
    {
        // Parse errors are already reported by the schema stage; the CMT pass must stay silent.
        var file = Path.Combine(Path.GetTempPath(), $"cmt-broken-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, "<entities><entity");

            Assert.Empty(new CmtDataSchemaValidator().ValidateFile(file));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void UpdateCompareFalse_ReportsError()
    {
        var results = Validate("""
            <entities>
              <entity name="talxis_configuration">
                <fields>
                  <field updateCompare="false" name="talxis_configurationid" type="guid" primaryKey="true" />
                </fields>
              </entity>
            </entities>
            """);

        Assert.Single(results);
    }
}
