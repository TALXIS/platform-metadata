using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtPackageValidatorTests
{
    private const string Schema = """
        <entities >
          <entity name="account" primaryidfield="accountid" primarynamefield="name">
            <fields>
              <field name="accountid" type="guid" primaryKey="true" />
              <field name="name" type="string" updateCompare="true" />
            </fields>
          </entity>
          <entity name="new_tag" primaryidfield="new_tagid" primarynamefield="new_name">
            <fields>
              <field name="new_tagid" type="guid" primaryKey="true" />
              <field name="new_name" type="string" updateCompare="true" />
              <field name="new_parent" type="entityreference" lookupType="account" />
            </fields>
            <relationships>
              <relationship name="new_tag_account" manyToMany="true" relatedEntityName="new_tag_account" m2mTargetEntity="account" m2mTargetEntityPrimaryKey="accountid" />
            </relationships>
          </entity>
        </entities>
        """;

    private static IReadOnlyList<ValidationResult> Validate(string data) =>
        new CmtPackageValidator().Validate(new CmtPackageXmlReader().Read(
            XDocument.Parse(Schema, LoadOptions.SetLineInfo),
            XDocument.Parse(data, LoadOptions.SetLineInfo)));

    [Fact]
    public void MatchingData_HasNoFindings()
    {
        var results = Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111">
                    <field name="name" value="Contoso" />
                  </record>
                </records>
              </entity>
              <entity name="new_tag">
                <records />
                <m2mrelationships>
                  <m2mrelationship sourceid="22222222-2222-2222-2222-222222222222" targetentityname="account" targetentitynameidfield="accountid" m2mrelationshipname="new_tag_account">
                    <targetids><targetid>11111111-1111-1111-1111-111111111111</targetid></targetids>
                  </m2mrelationship>
                </m2mrelationships>
              </entity>
            </entities>
            """);

        Assert.Empty(results);
    }

    [Fact]
    public void DataEntityNotInSchema_ReportsError()
    {
        var finding = Assert.Single(Validate("""
            <entities><entity name="contact"><records><record id="11111111-1111-1111-1111-111111111111" /></records></entity></entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtDataUndeclared, finding.Code);
        Assert.Contains("'contact'", finding.Message);
    }

    [Fact]
    public void DataFieldNotInSchema_ReportsOneErrorPerField()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="telephone1" value="1" /></record>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="telephone1" value="2" /></record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtDataUndeclared, finding.Code);
        Assert.Contains("account.telephone1", finding.Message);
        Assert.Contains("2 records", finding.Message);
        Assert.True(finding.Line > 0);
    }

    [Fact]
    public void LookupToEntityOutsidePackage_ReportsWarning()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111">
                    <field name="name" value="Contoso" lookupentity="systemuser" lookupentityname="Admin" />
                  </record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtDataLookupEntityUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void ManyToManyNotInSchema_ReportsErrors()
    {
        var results = Validate("""
            <entities>
              <entity name="account">
                <records />
                <m2mrelationships>
                  <m2mrelationship sourceid="11111111-1111-1111-1111-111111111111" targetentityname="contact" m2mrelationshipname="account_contact">
                    <targetids><targetid>22222222-2222-2222-2222-222222222222</targetid></targetids>
                  </m2mrelationship>
                </m2mrelationships>
              </entity>
            </entities>
            """);

        Assert.Equal(2, results.Count(r => r.Code == ValidationDiagnostics.CmtDataManyToManyUndeclared));
        Assert.Contains(results, r => r.Message.Contains("'account_contact'"));
        Assert.Contains(results, r => r.Message.Contains("targets entity 'contact'"));
    }

    [Fact]
    public void DataNamesDifferingOnlyByCase_ReportCaseMismatchWarningsNotErrors()
    {
        var results = Validate("""
            <entities>
              <entity name="Account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="name" value="Contoso" /></record>
                </records>
              </entity>
              <entity name="new_tag">
                <records>
                  <record id="22222222-2222-2222-2222-222222222222">
                    <field name="New_Name" value="Tag" />
                    <field name="new_parent" value="11111111-1111-1111-1111-111111111111" lookupentity="ACCOUNT" />
                  </record>
                </records>
                <m2mrelationships>
                  <m2mrelationship sourceid="22222222-2222-2222-2222-222222222222" targetentityname="Account" m2mrelationshipname="New_Tag_Account">
                    <targetids><targetid>11111111-1111-1111-1111-111111111111</targetid></targetids>
                  </m2mrelationship>
                </m2mrelationships>
              </entity>
            </entities>
            """);

        // entity, field, lookupentity, m2m relationship name and m2m target: five case-only matches.
        Assert.Equal(5, results.Count(r => r.Code == ValidationDiagnostics.CmtNameCaseMismatch));
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Warning, r.Severity));
        Assert.Contains(results, r => r.Message.Contains("entity 'Account' is declared as 'account'"));
        Assert.Contains(results, r => r.Message.Contains("'new_tag.New_Name'") && r.Message.Contains("'new_name'"));
        Assert.Contains(results, r => r.Message.Contains("'ACCOUNT'") && r.Message.Contains("'account'"));
        Assert.Contains(results, r => r.Message.Contains("'New_Tag_Account'") && r.Message.Contains("'new_tag_account'"));
    }

    [Fact]
    public void UnparseableTimestamp_ReportsError()
    {
        var finding = Assert.Single(Validate("""<entities timestamp="yesterday"><entity name="account"><records /></entity></entities>"""));

        Assert.Equal(ValidationDiagnostics.CmtDataTimestampInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("'yesterday'", finding.Message);
    }

    [Fact]
    public void CmtTimestamp_HasNoFinding()
    {
        Assert.Empty(Validate("""<entities timestamp="2021-08-16T12:15:05.4811021Z"><entity name="account"><records /></entity></entities>"""));
    }

    [Theory]
    [InlineData("real-export")]
    [InlineData("talxis-dialect")]
    [InlineData("live-export")]
    public void Fixtures_ProduceNoErrors(string fixture)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", fixture);
        var package = new CmtPackageXmlReader().Load(Path.Combine(root, "data_schema.xml"), Path.Combine(root, "data.xml"));
        Assert.Empty(package.LoadErrors);

        var results = new CmtDataSchemaValidator().Validate(package.Schema).Concat(new CmtPackageValidator().Validate(package)).ToList();

        // Warnings are expected (TALXIS omits etc, uses type="file", references entities outside the package); errors are not.
        Assert.Empty(results.Where(r => r.Severity == ValidationSeverity.Error));
        if (fixture == "talxis-dialect")
        {
            Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtRequiredAttributeMissing && r.Message.Contains("has no etc"));
            Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtFieldTypeNotImportable && r.Message.Contains("'file'"));
        }
    }
}
