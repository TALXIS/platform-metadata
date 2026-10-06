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
    public void EntityWithoutUpdateCompareButWithPrimaryName_ReportsWarning()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account" primaryidfield="accountid" primarynamefield="name">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" />
                  <field name="name" type="string" />
                </fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtEntityMissingUpdateCompare, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("primary name 'name'", finding.Message);
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
    public void ImportOrderNamingUndeclaredEntity_ReportsWarning()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>account</entityName><entityName>acount</entityName></entityImportOrder>
            </entities>
            """);

        var finding = Assert.Single(results);
        Assert.Equal(ValidationDiagnostics.CmtImportOrderEntityUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("acount", finding.Message);
    }

    [Fact]
    public void EntityMissingFromImportOrder_ReportsWarnings()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>contact</entityName></entityImportOrder>
            </entities>
            """);

        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtImportOrderEntityUndeclared && r.Severity == ValidationSeverity.Warning && r.Message.Contains("'contact'"));
        Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtImportOrderEntityUndeclared && r.Severity == ValidationSeverity.Warning && r.Message.Contains("'account'"));
    }

    [Fact]
    public void ImportOrderWithDifferentCase_ReportsCaseMismatchWarning()
    {
        var results = Validate($"""
            <entities>
              {ValidEntity}
              <entityImportOrder><entityName>Account</entityName></entityImportOrder>
            </entities>
            """);

        // The case-only match is a warning, not "undeclared"; the entity is still missing from the order (exact match), so TXM007 warns too.
        var mismatch = Assert.Single(results, r => r.Code == ValidationDiagnostics.CmtNameCaseMismatch);
        Assert.Equal(ValidationSeverity.Warning, mismatch.Severity);
        Assert.Contains("'Account'", mismatch.Message);
        Assert.Contains("'account'", mismatch.Message);
        Assert.DoesNotContain(results, r => r.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void SchemaWithoutImportOrder_IsNotChecked()
    {
        Assert.Empty(Validate($"<entities>{ValidEntity}</entities>"));
    }

    [Theory]
    [InlineData("""<entity name="account"><fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields></entity>""", "no primaryidfield")]
    [InlineData("""<entity name="account" primaryidfield="accountid"><fields><field name="name" type="string" updateCompare="true" /></fields></entity>""", "is not declared")]
    [InlineData("""<entity name="account" primaryidfield="accountid"><fields><field name="accountid" type="string" primaryKey="true" updateCompare="true" /></fields></entity>""", "instead of 'guid'")]
    public void InvalidPrimaryIdField_ReportsError(string entity, string expectedText)
    {
        var finding = Assert.Single(Validate($"<entities>{entity}</entities>"));
        Assert.Equal(ValidationDiagnostics.CmtPrimaryIdFieldInvalid, finding.Code);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public void PrimaryIdFieldWithoutPrimaryKeyFlag_ReportsWarning()
    {
        var finding = Assert.Single(Validate("""<entities><entity name="account" primaryidfield="accountid"><fields><field name="accountid" type="guid" updateCompare="true" /></fields></entity></entities>"""));

        Assert.Equal(ValidationDiagnostics.CmtPrimaryIdFieldInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("primaryKey", finding.Message);
    }

    [Fact]
    public void UndeclaredPrimaryNameField_ReportsWarning()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account" primaryidfield="accountid" primarynamefield="name">
                <fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtPrimaryNameFieldUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void NamesThatAreNotLowercase_ReportErrors()
    {
        var results = Validate("""
            <entities>
              <entity name="CMTL_child" primaryidfield="cmtl_childid">
                <fields>
                  <field name="cmtl_childid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="cmtl_String" type="string" />
                </fields>
              </entity>
            </entities>
            """);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal((ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error), (r.Code, r.Severity)));
        Assert.Contains(results, r => r.Message.Contains("entity 'CMTL_child' is not lowercase"));
        Assert.Contains(results, r => r.Message.Contains("field 'CMTL_child.cmtl_String' is not lowercase"));
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

    [Theory]
    [InlineData("Guid", "instead of 'guid'")]
    [InlineData("lookup", "not a CMT field type")]
    [InlineData("customer", "Use 'entityreference'")]
    [InlineData("unknown", "cannot import")]
    [InlineData("", "has no type")]
    public void FieldTypeCmtCannotImport_ReportsError(string type, string expectedText)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="account" primaryidfield="accountid">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="x" type="{type}" />
                </fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtFieldTypeNotImportable, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("account.x", finding.Message);
        Assert.Contains(expectedText, finding.Message);
    }

    [Theory]
    [InlineData("file", "use 'filedata'")]
    [InlineData("bigint", "drops the values")]
    public void FileSynonymAndBigInt_ReportWarning(string type, string expectedText)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="account" primaryidfield="accountid">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="doc" type="{type}" />
                </fields>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtFieldTypeNotImportable, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public void ManyToManyIntersectFieldTypes_AreChecked()
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="account" displayname="Account" etc="1" disableplugins="false" primaryidfield="accountid">
                <fields><field displayname="Id" name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields>
                <relationships>
                  <relationship name="account_contact" manyToMany="true">
                    <fields><field name="accountid" type="Guid" primaryKey="true" /><field name="contactid" type="guid" /></fields>
                  </relationship>
                </relationships>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtFieldTypeNotImportable, finding.Code);
        Assert.Contains("account/account_contact.accountid", finding.Message);
    }

    [Fact]
    public void AttributesCmtIgnores_AbsentHaveNoFinding()
    {
        // No etc, displayname or disableplugins: CMT imports and exports without them.
        Assert.Empty(Validate("""
            <entities>
              <entity name="account" primaryidfield="accountid">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="telephone1" type="string" />
                </fields>
              </entity>
            </entities>
            """));
    }

    [Theory]
    [InlineData("""<entities dateMode="Absolute">{0}</entities>""", "root")]
    [InlineData("""<entities>{1}</entities>""", "account.d")]
    public void InvalidDateMode_ReportsError(string template, string subject)
    {
        var withBadField = """
            <entity name="account" primaryidfield="accountid">
              <fields>
                <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                <field name="d" type="datetime" dateMode="daily" />
              </fields>
            </entity>
            """;
        var finding = Assert.Single(Validate(template.Replace("{0}", ValidEntity).Replace("{1}", withBadField)));

        Assert.Equal(ValidationDiagnostics.CmtDateModeInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains(subject, finding.Message);
    }

    [Fact]
    public void ValidDateModes_HaveNoFinding()
    {
        Assert.Empty(Validate("""
            <entities dateMode="relativeDaily">
              <entity name="account" primaryidfield="accountid">
                <fields>
                  <field name="accountid" type="guid" primaryKey="true" updateCompare="true" />
                  <field name="d" type="datetime" dateMode="relative" />
                </fields>
              </entity>
            </entities>
            """));
    }

    [Theory]
    [InlineData("&lt;filter&gt;&lt;condition /&gt;&lt;/filter&gt;", "instead of <fetch>")]
    [InlineData("&lt;fetch&gt;", "not well-formed")]
    public void FilterThatIsNotFetchXml_ReportsWarning(string filter, string expectedText)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="account" primaryidfield="accountid">
                <fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields>
                <filter>{filter}</filter>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtFilterNotFetchXml, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public void FetchXmlFilter_HasNoFinding()
    {
        Assert.Empty(Validate("""
            <entities>
              <entity name="account" primaryidfield="accountid">
                <fields><field name="accountid" type="guid" primaryKey="true" updateCompare="true" /></fields>
                <filter>&lt;fetch&gt;&lt;entity name="account" /&gt;&lt;/fetch&gt;</filter>
              </entity>
            </entities>
            """));
    }
}
