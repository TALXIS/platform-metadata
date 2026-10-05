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
              <entity name="talxis_configuration" primaryidfield="talxis_configurationid" displayname="Configuration">
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
              <entity name="talxis_configuration" primaryidfield="talxis_configurationid" displayname="Configuration">
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
              <entity name="talxis_good" primaryidfield="talxis_goodid">
                <fields>
                  <field updateCompare="true" name="talxis_goodid" type="guid" primaryKey="true" />
                </fields>
              </entity>
              <entity name="talxis_bad" primaryidfield="talxis_badid">
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
              <entity name="talxis_configuration" primaryidfield="talxis_configurationid">
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
              <entity name="talxis_configuration" primaryidfield="talxis_configurationid">
                <fields>
                  <field updateCompare="false" name="talxis_configurationid" type="guid" primaryKey="true" />
                </fields>
              </entity>
            </entities>
            """);

        Assert.Single(results);
    }

    private const string ValidEntity = """
        <entity name="account" primaryidfield="accountid" primarynamefield="name">
          <fields>
            <field name="accountid" type="guid" primaryKey="true" />
            <field name="name" type="string" updateCompare="true" />
          </fields>
        </entity>
        """;

    [Fact]
    public void ValidSchemaWithImportOrder_HasNoFindings()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>account</entityName></entityImportOrder>
            </entities>
            """);

        Assert.Empty(results);
    }

    [Fact]
    public void ImportOrderNamingUndeclaredEntity_ReportsError()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>account</entityName><entityName>acount</entityName></entityImportOrder>
            </entities>
            """);

        var finding = Assert.Single(results);
        Assert.Equal(ValidationDiagnostics.CmtImportOrderEntityUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("acount", finding.Message);
    }

    [Fact]
    public void EntityMissingFromImportOrder_ReportsWarning()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>contact</entityName></entityImportOrder>
            </entities>
            """);

        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtImportOrderEntityUndeclared && r.Severity == ValidationSeverity.Error && r.Message.Contains("'contact'"));
        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtImportOrderEntityUndeclared && r.Severity == ValidationSeverity.Warning && r.Message.Contains("'account'"));
    }

    [Fact]
    public void SchemaWithoutImportOrder_IsNotChecked()
    {
        Assert.Empty(Validate($"<entities>{ValidEntity}</entities>"));
    }

    [Theory]
    [InlineData("""<entity name="account"><fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields></entity>""", "no primaryidfield")]
    [InlineData("""<entity name="account" primaryidfield="accountid"><fields><field name="name" type="string" updateCompare="true" /></fields></entity>""", "is not declared")]
    [InlineData("""<entity name="account" primaryidfield="accountid"><fields><field name="accountid" type="guid" updateCompare="true" /></fields></entity>""", "primaryKey")]
    [InlineData("""<entity name="account" primaryidfield="accountid"><fields><field name="accountid" type="string" primaryKey="true" updateCompare="true" /></fields></entity>""", "instead of 'guid'")]
    public void InvalidPrimaryIdField_ReportsError(string entity, string expectedText)
    {
        var finding = Assert.Single(Validate($"<entities>{entity}</entities>"));
        Assert.Equal(ValidationDiagnostics.CmtPrimaryIdFieldInvalid, finding.Code);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public void UndeclaredPrimaryNameField_ReportsError()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account" primaryidfield="accountid" primarynamefield="name">
                <fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtPrimaryNameFieldUndeclared, finding.Code);
    }

    [Theory]
    [InlineData("entityreference")]
    [InlineData("customer")]
    [InlineData("owner")]
    public void LookupWithoutLookupType_ReportsError(string type)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="contact" primaryidfield="contactid">
                <fields>
                  <field name="contactid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="parentid" type="{type}" />
                </fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtLookupTypeMissing, finding.Code);
        Assert.Contains("contact.parentid", finding.Message);
        Assert.True(finding.Line > 0);
    }

    [Fact]
    public void DuplicateEntitiesAndFields_ReportError()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entity name="account" primaryidfield="accountid">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="accountid" type="guid" primaryKey="true" />
                </fields>
              </entity>
            </entities>
            """);

        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtDuplicateName && r.Message.Contains("entity 'account' more than once"));
        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtDuplicateName && r.Message.Contains("field 'accountid' more than once"));
    }
}
