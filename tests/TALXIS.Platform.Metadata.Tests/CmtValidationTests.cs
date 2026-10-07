using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtValidationTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", "exhaustive");

    // One row per rule branch: a single edit to the live-proven package and the finding it must add. The rows and
    // their severities encode behaviour observed by importing each edit with Microsoft CMT into Dataverse.
    public static TheoryData<string, Action<XDocument, XDocument>, string, ValidationSeverity> Rules => new()
    {
        { "no updateCompare, matched on primary name", (s, d) => Field(s, "cmtl_parent", "cmtl_name").Attribute("updateCompare")!.Remove(), ValidationDiagnostics.CmtEntityMissingUpdateCompare, ValidationSeverity.Warning },
        { "no updateCompare and no primarynamefield", (s, d) => { Field(s, "cmtl_parent", "cmtl_name").Attribute("updateCompare")!.Remove(); Entity(s, "cmtl_parent").Attribute("primarynamefield")!.Remove(); }, ValidationDiagnostics.CmtEntityMissingUpdateCompare, ValidationSeverity.Error },
        { "import order names an undeclared entity", (s, d) => s.Root!.Element("entityImportOrder")!.Add(new XElement("entityName", "cmtl_ghost")), ValidationDiagnostics.CmtImportOrderEntityUndeclared, ValidationSeverity.Warning },
        { "entity missing from import order", (s, d) => s.Root!.Element("entityImportOrder")!.Elements().Last().Remove(), ValidationDiagnostics.CmtImportOrderEntityUndeclared, ValidationSeverity.Warning },
        { "primaryidfield absent", (s, d) => Entity(s, "cmtl_child").Attribute("primaryidfield")!.Remove(), ValidationDiagnostics.CmtPrimaryIdFieldInvalid, ValidationSeverity.Error },
        { "primaryidfield not declared", (s, d) => Entity(s, "cmtl_child").SetAttributeValue("primaryidfield", "cmtl_childidx"), ValidationDiagnostics.CmtPrimaryIdFieldInvalid, ValidationSeverity.Error },
        { "primaryidfield not of type guid", (s, d) => Field(s, "cmtl_child", "cmtl_childid").SetAttributeValue("type", "string"), ValidationDiagnostics.CmtPrimaryIdFieldInvalid, ValidationSeverity.Error },
        { "primary key without primaryKey", (s, d) => Field(s, "cmtl_child", "cmtl_childid").Attribute("primaryKey")!.Remove(), ValidationDiagnostics.CmtPrimaryIdFieldInvalid, ValidationSeverity.Warning },
        { "primarynamefield not declared", (s, d) => Entity(s, "account").SetAttributeValue("primarynamefield", "nosuch"), ValidationDiagnostics.CmtPrimaryNameFieldUndeclared, ValidationSeverity.Warning },
        { "field declared twice", (s, d) => Field(s, "account", "telephone1").AddAfterSelf(new XElement(Field(s, "account", "telephone1"))), ValidationDiagnostics.CmtDuplicateName, ValidationSeverity.Error },
        { "entity declared twice", (s, d) => Entity(s, "account").AddAfterSelf(new XElement(Entity(s, "account"))), ValidationDiagnostics.CmtDuplicateName, ValidationSeverity.Error },
        { "data entity not declared", (s, d) => Entity(d, "cmtl_parent").SetAttributeValue("name", "cmtl_parent2"), ValidationDiagnostics.CmtDataUndeclared, ValidationSeverity.Error },
        { "data field not declared", (s, d) => Field(s, "cmtl_child", "cmtl_string").Remove(), ValidationDiagnostics.CmtDataUndeclared, ValidationSeverity.Error },
        { "lookupentity not in the package", (s, d) => Value(d, "cmtl_child", "cmtl_parentid").SetAttributeValue("lookupentity", "cmtl_ghost"), ValidationDiagnostics.CmtDataLookupEntityUndeclared, ValidationSeverity.Warning },
        { "m2mrelationshipname not declared", (s, d) => ManyToMany(d).SetAttributeValue("m2mrelationshipname", "cmtl_xx"), ValidationDiagnostics.CmtDataManyToManyUndeclared, ValidationSeverity.Error },
        { "N:N target entity not in the package", (s, d) => ManyToMany(d).SetAttributeValue("targetentityname", "cmtl_ghost"), ValidationDiagnostics.CmtDataManyToManyUndeclared, ValidationSeverity.Warning },
        { "second association targets an undeclared entity", (s, d) => { var second = new XElement(ManyToMany(d)); second.SetAttributeValue("targetentityname", "cmtl_ghost"); ManyToMany(d).AddAfterSelf(second); }, ValidationDiagnostics.CmtDataManyToManyUndeclared, ValidationSeverity.Warning },
        { "targetentitynameidfield not the target's primary id", (s, d) => ManyToMany(d).SetAttributeValue("targetentitynameidfield", "cmtl_childidx"), ValidationDiagnostics.CmtDataManyToManyTargetIdFieldInvalid, ValidationSeverity.Error },
        { "schema entity name not lowercase", (s, d) => Entity(s, "cmtl_child").SetAttributeValue("name", "CMTL_child"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "schema field name not lowercase", (s, d) => Field(s, "cmtl_child", "cmtl_string").SetAttributeValue("name", "cmtl_String"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "N:N intersect field name not lowercase", (s, d) => Entity(s, "cmtl_parent").Element("relationships")!.Element("relationship")!.Add(new XElement("fields", new XElement("field", new XAttribute("name", "CMTL_childid"), new XAttribute("type", "guid")))), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "data entity differs only by case", (s, d) => Entity(d, "cmtl_child").SetAttributeValue("name", "CMTL_child"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "data field differs only by case", (s, d) => Value(d, "cmtl_child", "cmtl_string").SetAttributeValue("name", "cmtl_String"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Warning },
        { "lookupentity differs only by case", (s, d) => Value(d, "cmtl_child", "cmtl_parentid").SetAttributeValue("lookupentity", "CMTL_parent"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "m2mrelationshipname differs only by case", (s, d) => ManyToMany(d).SetAttributeValue("m2mrelationshipname", "CMTL_cmtl_parent_cmtl_child"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Error },
        { "N:N target entity differs only by case", (s, d) => ManyToMany(d).SetAttributeValue("targetentityname", "CMTL_child"), ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Warning },
        { "import order name differs only by case", (s, d) => s.Root!.Element("entityImportOrder")!.Elements().First().Value = "Account", ValidationDiagnostics.CmtNameCaseMismatch, ValidationSeverity.Warning },
        { "field without type", (s, d) => Field(s, "cmtl_child", "cmtl_string").Attribute("type")!.Remove(), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Error },
        { "type unknown", (s, d) => Field(s, "cmtl_child", "cmtl_number").SetAttributeValue("type", "unknown"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Error },
        { "type customer", (s, d) => Field(s, "cmtl_child", "cmtl_customerid").SetAttributeValue("type", "customer"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Error },
        { "type capitalised", (s, d) => Field(s, "cmtl_child", "cmtl_string").SetAttributeValue("type", "String"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Error },
        { "type outside the CMT vocabulary", (s, d) => Field(s, "cmtl_child", "cmtl_string").SetAttributeValue("type", "text"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Error },
        { "type bigint", (s, d) => Field(s, "cmtl_child", "cmtl_number").SetAttributeValue("type", "bigint"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Warning },
        { "type file", (s, d) => Field(s, "cmtl_child", "cmtl_file").SetAttributeValue("type", "file"), ValidationDiagnostics.CmtFieldTypeNotImportable, ValidationSeverity.Warning },
        {
            "import order puts a child before its parent",
            (s, d) => { var names = s.Root!.Element("entityImportOrder")!.Elements().ToList(); names[2].Value = "cmtl_child"; names[3].Value = "cmtl_parent"; },
            ValidationDiagnostics.CmtImportOrderChildBeforeParent,
            ValidationSeverity.Warning
        },
        { "record id repeated", (s, d) => Record(d, "cmtl_child", 1).SetAttributeValue("id", Record(d, "cmtl_child", 0).Attribute("id")!.Value), ValidationDiagnostics.CmtRecordIdentityInvalid, ValidationSeverity.Error },
        { "primary-id value differs from record id", (s, d) => Value(d, "cmtl_child", "cmtl_childid").SetAttributeValue("value", Guid.Empty.ToString()), ValidationDiagnostics.CmtRecordIdentityInvalid, ValidationSeverity.Error },
        { "primary-id value empty", (s, d) => Value(d, "cmtl_child", "cmtl_childid").SetAttributeValue("value", string.Empty), ValidationDiagnostics.CmtRecordIdentityInvalid, ValidationSeverity.Error },
        { "primary-id value not a GUID", (s, d) => Value(d, "cmtl_child", "cmtl_childid").SetAttributeValue("value", "not-a-guid"), ValidationDiagnostics.CmtRecordIdentityInvalid, ValidationSeverity.Error },
        { "primary-id field absent", (s, d) => Value(d, "cmtl_child", "cmtl_childid").Remove(), ValidationDiagnostics.CmtRecordIdentityInvalid, ValidationSeverity.Warning },
        { "root dateMode invalid", (s, d) => s.Root!.SetAttributeValue("dateMode", "bogus"), ValidationDiagnostics.CmtDateModeInvalid, ValidationSeverity.Error },
        { "field dateMode invalid", (s, d) => Field(s, "cmtl_child", "cmtl_datetime").SetAttributeValue("dateMode", "Relative"), ValidationDiagnostics.CmtDateModeInvalid, ValidationSeverity.Error },
        { "timestamp invalid", (s, d) => d.Root!.SetAttributeValue("timestamp", "notadate"), ValidationDiagnostics.CmtDataTimestampInvalid, ValidationSeverity.Error },
        { "filter not well-formed", (s, d) => Entity(s, "cmtl_child").Element("filter")!.Value = "<fetch>", ValidationDiagnostics.CmtFilterNotFetchXml, ValidationSeverity.Warning },
        { "filter without a fetch root", (s, d) => Entity(s, "cmtl_child").Element("filter")!.Value = "<query />", ValidationDiagnostics.CmtFilterNotFetchXml, ValidationSeverity.Warning },
        { "lookupentity missing", (s, d) => Value(d, "cmtl_child", "cmtl_parentid").Attribute("lookupentity")!.Remove(), ValidationDiagnostics.CmtDataLookupIncomplete, ValidationSeverity.Warning },
        { "lookupentityname missing", (s, d) => Value(d, "cmtl_child", "cmtl_customerid").Attribute("lookupentityname")!.Remove(), ValidationDiagnostics.CmtDataLookupIncomplete, ValidationSeverity.Warning },
        { "lookupentity names the wrong table", (s, d) => Value(d, "cmtl_child", "cmtl_parentid").SetAttributeValue("lookupentity", "cmtl_child"), ValidationDiagnostics.CmtDataLookupIncomplete, ValidationSeverity.Warning },
        { "two records share the updateCompare value", (s, d) => Value(d, "cmtl_child", "cmtl_name").SetAttributeValue("value", "CMTLAB EX Child 2"), ValidationDiagnostics.CmtDataDuplicateMatchKey, ValidationSeverity.Warning },
        { "two records share the primary name, no updateCompare", (s, d) => { Field(s, "cmtl_child", "cmtl_name").Attribute("updateCompare")!.Remove(); Value(d, "cmtl_child", "cmtl_name").SetAttributeValue("value", "CMTLAB EX Child 2"); }, ValidationDiagnostics.CmtDataDuplicateMatchKey, ValidationSeverity.Warning },
        { "bool 1", (s, d) => Value(d, "cmtl_child", "cmtl_bool").SetAttributeValue("value", "1"), ValidationDiagnostics.CmtDataValueInvalid, ValidationSeverity.Warning },
        { "money with currency symbol", (s, d) => Value(d, "cmtl_child", "cmtl_money").SetAttributeValue("value", "$12.50"), ValidationDiagnostics.CmtDataValueInvalid, ValidationSeverity.Warning },
        { "choice not an integer", (s, d) => Value(d, "cmtl_child", "cmtl_choice").SetAttributeValue("value", "71000000.5"), ValidationDiagnostics.CmtDataValueInvalid, ValidationSeverity.Warning },
        { "multichoice brackets without sentinels", (s, d) => Value(d, "cmtl_child", "cmtl_multichoice").SetAttributeValue("value", "[71000010,71000012]"), ValidationDiagnostics.CmtDataValueInvalid, ValidationSeverity.Warning },
        { "datetime not parseable", (s, d) => Value(d, "cmtl_child", "cmtl_dateonly").SetAttributeValue("value", "next monday"), ValidationDiagnostics.CmtDataValueInvalid, ValidationSeverity.Warning },
    };

    // Edits CMT imports without loss.
    public static TheoryData<string, Action<XDocument, XDocument>> Accepted => new()
    {
        { "empty multichoice in export form", (s, d) => Value(d, "cmtl_child", "cmtl_multichoice").SetAttributeValue("value", "[-1,-1]") },
        { "multichoice as plain list", (s, d) => Value(d, "cmtl_child", "cmtl_multichoice").SetAttributeValue("value", "71000010,71000012") },
        { "bool in lowercase", (s, d) => Value(d, "cmtl_child", "cmtl_bool").SetAttributeValue("value", "false") },
        { "lookupType *", (s, d) => Field(s, "cmtl_child", "cmtl_regardingid").SetAttributeValue("lookupType", "*") },
        { "no entityImportOrder", (s, d) => s.Root!.Element("entityImportOrder")!.Remove() },
        { "no timestamp", (s, d) => d.Root!.Attribute("timestamp")!.Remove() },
        { "displayname '#' placeholder", (s, d) => Entity(s, "account").SetAttributeValue("displayname", "#") },
        { "Liquid value in a renderliquid entity", (s, d) => { Entity(s, "cmtl_child").SetAttributeValue("renderliquid", "true"); Value(d, "cmtl_child", "cmtl_number").SetAttributeValue("value", "{{ 40 | plus: 2 }}"); } },
    };

    [Fact]
    public void Fixture_ReportsOnlyTheUndeclaredContactFullName()
    {
        var results = Validate((s, d) => { });

        // CMT's own export declares contact primarynamefield="fullname" without the computed fullname column.
        var finding = Assert.Single(results);
        Assert.Equal(ValidationDiagnostics.CmtPrimaryNameFieldUndeclared, finding.Code);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
        Assert.Contains("'fullname'", finding.Message);
    }

    [Theory]
    [MemberData(nameof(Rules))]
    public void Rule_ReportsFinding(string scenario, Action<XDocument, XDocument> edit, string code, ValidationSeverity severity)
    {
        var baseline = Validate((s, d) => { }).Select(r => r.Message).ToList();

        var added = Validate(edit).Where(r => !baseline.Contains(r.Message)).ToList();

        Assert.True(added.Any(r => r.Code == code && r.Severity == severity),
            $"{scenario}: expected {code} {severity}, got {string.Join("; ", added.Select(r => $"{r.Code} {r.Severity}"))}");
    }

    [Theory]
    [MemberData(nameof(Accepted))]
    public void AcceptedEdit_ReportsNothing(string scenario, Action<XDocument, XDocument> edit)
    {
        var baseline = Validate((s, d) => { }).Select(r => r.Message).ToList();

        var added = Validate(edit).Where(r => !baseline.Contains(r.Message)).ToList();

        Assert.True(added.Count == 0, $"{scenario}: {string.Join("; ", added.Select(r => r.Message))}");
    }

    [Fact]
    public void ThousandsSeparator_ExplainsCultureDependence()
    {
        var results = Validate((s, d) => Value(d, "cmtl_child", "cmtl_number").SetAttributeValue("value", "1,234"));

        var finding = Assert.Single(results, r => r.Code == ValidationDiagnostics.CmtDataValueInvalid);
        Assert.Contains("thousands separator", finding.Message);
        Assert.Contains("culture", finding.Message);
    }

    // Malformed XML is reported once, by the XSD stage, not again by the rule stage.
    [Theory]
    [InlineData("<entities><entity name=\"a\">", null)]
    [InlineData(null, "File not found")]
    [InlineData("<entities><entity name=\"a\"><fields><field name=\"aid\" type=\"guid\" updateCompare=\"false\" /></fields></entity></entities>", "updateCompare")]
    public void ValidateFile_HandlesBrokenAndMissingFiles(string? content, string? expectedText)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmt-file-{Guid.NewGuid():N}.xml");
        if (content != null) File.WriteAllText(path, content);
        try
        {
            var results = new CmtDataSchemaValidator().ValidateFile(path);

            if (expectedText == null)
                Assert.Empty(results);
            else
                Assert.Contains(results, r => r.Severity == ValidationSeverity.Error && r.Message.Contains(expectedText));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateXml_SkipsDataFilesAndOtherXml()
    {
        var validator = new CmtDataSchemaValidator();

        Assert.Empty(validator.ValidateXml(XDocument.Load(Path.Combine(FixturePath, "data.xml"))));
        Assert.Empty(validator.ValidateXml(XDocument.Parse("<ImportExportXml><entities /></ImportExportXml>")));
        Assert.Contains(validator.ValidateXml(XDocument.Parse("<entities><entity name=\"a\"><fields /></entity></entities>"), "data_schema.xml"),
            r => r.Code == ValidationDiagnostics.CmtEntityMissingUpdateCompare && r.FilePath == "data_schema.xml");
    }

    [Theory]
    [InlineData("""<entities><entity name="a"><fields /><filter>x</filter><relationships /></entity></entities>""")]
    [InlineData("""<entities dateMode="Absolute"><entity name="a"><fields /></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><fields><field name="d" type="datetime" dateMode="daily" /></fields></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><fields /><bogus /></entity></entities>""")]
    [InlineData("""<entities><entity name="a"><fields><field name="aid" type="guid" updateCompare="True" /></fields></entity></entities>""")]
    public void Xsd_RejectsShapeCmtCannotRead(string xml)
    {
        Assert.Contains(new SchemaValidator().ValidateXml(XDocument.Parse(xml), "data_schema.xml"), r => r.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void RepeatedRecordId_SaysWhichFieldsTheCopiesDisagreeOn()
    {
        var differing = Validate((s, d) =>
        {
            var copy = new XElement(Record(d, "cmtl_child", 0));
            copy.Elements("field").First(f => f.Attribute("name")?.Value == "cmtl_string").SetAttributeValue("value", "changed in the second copy");
            Record(d, "cmtl_child", 0).AddAfterSelf(copy);
        });
        var identical = Validate((s, d) => Record(d, "cmtl_child", 0).AddAfterSelf(new XElement(Record(d, "cmtl_child", 0))));
        // Compared like package merging: a lookup name the importer falls back on counts too.
        var lookupName = Validate((s, d) =>
        {
            var copy = new XElement(Record(d, "cmtl_child", 0));
            copy.Elements("field").First(f => f.Attribute("name")?.Value == "cmtl_parentid").SetAttributeValue("lookupentityname", "Other parent");
            Record(d, "cmtl_child", 0).AddAfterSelf(copy);
        });

        Assert.Contains(differing, r => r.Code == ValidationDiagnostics.CmtRecordIdentityInvalid && r.Message.Contains("copies differ in cmtl_string"));
        Assert.Contains(identical, r => r.Code == ValidationDiagnostics.CmtRecordIdentityInvalid && r.Severity == ValidationSeverity.Error && r.Message.Contains("identical copies"));
        Assert.Contains(lookupName, r => r.Code == ValidationDiagnostics.CmtRecordIdentityInvalid && r.Message.Contains("copies differ in cmtl_parentid"));
    }

    private static IReadOnlyList<ValidationResult> Validate(Action<XDocument, XDocument> edit)
    {
        var schema = XDocument.Load(Path.Combine(FixturePath, "data_schema.xml"), LoadOptions.SetLineInfo);
        var data = XDocument.Load(Path.Combine(FixturePath, "data.xml"), LoadOptions.SetLineInfo);
        edit(schema, data);

        var package = new CmtPackageXmlReader().Read(schema, data);
        Assert.Empty(package.LoadErrors);
        return new CmtDataSchemaValidator().Validate(package.Schema).Concat(new CmtPackageValidator().Validate(package)).ToList();
    }

    private static XElement Entity(XDocument document, string name) =>
        document.Root!.Elements("entity").Single(e => e.Attribute("name")?.Value == name);

    private static XElement Field(XDocument schema, string entity, string name) =>
        Entity(schema, entity).Element("fields")!.Elements("field").Single(f => f.Attribute("name")?.Value == name);

    private static XElement Record(XDocument data, string entity, int index) =>
        Entity(data, entity).Element("records")!.Elements("record").ElementAt(index);

    private static XElement Value(XDocument data, string entity, string field) =>
        Record(data, entity, 0).Elements("field").Single(f => f.Attribute("name")?.Value == field);

    private static XElement ManyToMany(XDocument data) =>
        Entity(data, "cmtl_parent").Element("m2mrelationships")!.Element("m2mrelationship")!;
}
