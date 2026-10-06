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
                    <field name="accountid" value="11111111-1111-1111-1111-111111111111" />
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
                  <record id="11111111-1111-1111-1111-111111111111"><field name="accountid" value="11111111-1111-1111-1111-111111111111" /><field name="telephone1" value="1" /></record>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="accountid" value="22222222-2222-2222-2222-222222222222" /><field name="telephone1" value="2" /></record>
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
                    <field name="accountid" value="11111111-1111-1111-1111-111111111111" />
                    <field name="name" value="Contoso" lookupentity="contact" lookupentityname="Jane" />
                  </record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtDataLookupEntityUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void LookupToSystemTable_HasNoFinding()
    {
        // Every CMT export carries ownerid/createdby/transactioncurrencyid lookups to tables outside the package.
        Assert.Empty(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="accountid" value="11111111-1111-1111-1111-111111111111" /><field name="name" value="Contoso" lookupentity="systemuser" lookupentityname="Admin" /></record>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="accountid" value="22222222-2222-2222-2222-222222222222" /><field name="name" value="Fabrikam" lookupentity="transactioncurrency" lookupentityname="Euro" /></record>
                </records>
              </entity>
            </entities>
            """));
    }

    [Theory]
    [InlineData("""value="11111111-1111-1111-1111-111111111111" lookupentityname="Contoso" """, "has no lookupentity")]
    [InlineData("""value="11111111-1111-1111-1111-111111111111" lookupentity="account" """, "has no lookupentityname")]
    [InlineData("""value="11111111-1111-1111-1111-111111111111" lookupentity="contact" lookupentityname="Jane" """, "points to 'contact', which is not in its lookupType 'account'")]
    public void IncompleteLookup_ReportsOneWarningPerFieldAndProblem(string attributes, string expectedText)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="new_tag">
                <records>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="new_tagid" value="22222222-2222-2222-2222-222222222222" /><field name="new_parent" {attributes}/></record>
                  <record id="33333333-3333-3333-3333-333333333333"><field name="new_tagid" value="33333333-3333-3333-3333-333333333333" /><field name="new_parent" {attributes}/></record>
                </records>
              </entity>
            </entities>
            """).Where(r => r.Code != ValidationDiagnostics.CmtDataLookupEntityUndeclared));

        Assert.Equal(ValidationDiagnostics.CmtDataLookupIncomplete, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("'new_tag.new_parent' (2 records)", finding.Message);
        Assert.Contains(expectedText, finding.Message);
    }

    [Fact]
    public void CompleteLookupOrEmptyValue_HasNoFinding()
    {
        Assert.Empty(Validate("""
            <entities>
              <entity name="new_tag">
                <records>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="new_tagid" value="22222222-2222-2222-2222-222222222222" /><field name="new_parent" value="11111111-1111-1111-1111-111111111111" lookupentity="account" lookupentityname="Contoso" /></record>
                  <record id="33333333-3333-3333-3333-333333333333"><field name="new_tagid" value="33333333-3333-3333-3333-333333333333" /><field name="new_parent" value="" /></record>
                </records>
              </entity>
            </entities>
            """));
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
    public void DataNamesDifferingOnlyByCase_ReportCaseMismatches()
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

        // entity, field, lookupentity, m2m relationship name and m2m target: five case-only matches. CMT fails the
        // import on the entity (error); it skips the others (warnings).
        Assert.Equal(5, results.Count(r => r.Code == ValidationDiagnostics.CmtNameCaseMismatch));
        var error = Assert.Single(results, r => r.Severity == ValidationSeverity.Error);
        Assert.Contains("entity 'Account' is declared as 'account'", error.Message);
        Assert.Contains(results, r => r.Message.Contains("'new_tag.New_Name'") && r.Message.Contains("'new_name'"));
        Assert.Contains(results, r => r.Message.Contains("'ACCOUNT'") && r.Message.Contains("'account'"));
        Assert.Contains(results, r => r.Message.Contains("'New_Tag_Account'") && r.Message.Contains("'new_tag_account'"));
    }

    [Theory]
    [InlineData("""<field name="accountid" value="" />""", "empty 'accountid' value")]
    [InlineData("""<field name="accountid" value="not-a-guid" />""", "not a GUID")]
    [InlineData("""<field name="accountid" value="99999999-9999-9999-9999-999999999999" />""", "differs from the record id")]
    public void PrimaryIdFieldValueThatIsNotTheRecordId_ReportsError(string idField, string expectedText)
    {
        var finding = Assert.Single(Validate($"""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111">{idField}<field name="name" value="Contoso" /></record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtRecordIdentityInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains(expectedText, finding.Message);
        Assert.Contains("1 record(s)", finding.Message);
    }

    [Fact]
    public void RecordsWithoutPrimaryIdField_ReportOneWarning()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="name" value="Contoso" /></record>
                  <record id="22222222-2222-2222-2222-222222222222"><field name="name" value="Fabrikam" /></record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtRecordIdentityInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("2 record(s) have no 'accountid' field", finding.Message);
    }

    [Fact]
    public void DuplicateRecordId_ReportsError()
    {
        var finding = Assert.Single(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="accountid" value="11111111-1111-1111-1111-111111111111" /></record>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="accountid" value="11111111-1111-1111-1111-111111111111" /></record>
                </records>
              </entity>
            </entities>
            """));

        Assert.Equal(ValidationDiagnostics.CmtRecordIdentityInvalid, finding.Code);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("2 times", finding.Message);
    }

    [Fact]
    public void LiquidTemplateInPrimaryIdField_HasNoFinding()
    {
        Assert.Empty(Validate("""
            <entities>
              <entity name="account">
                <records>
                  <record id="11111111-1111-1111-1111-111111111111"><field name="accountid" value="{% randomguid %}" /></record>
                </records>
              </entity>
            </entities>
            """));
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

        // Warnings are expected (TALXIS uses type="file", references entities outside the package); errors are not.
        Assert.Empty(results.Where(r => r.Severity == ValidationSeverity.Error));
        if (fixture == "talxis-dialect")
            Assert.Contains(results, r => r.Code == ValidationDiagnostics.CmtFieldTypeNotImportable && r.Message.Contains("'file'"));
    }
}
