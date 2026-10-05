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
}
