using System.Text;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtPackageRoundtripTests : IDisposable
{
    // A real CMT export, imported into Dataverse without errors; it covers every type CMT handles.
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", "exhaustive");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cmt-roundtrip-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void LoadDirectory_ReadsEveryCmtShape()
    {
        var package = new CmtPackageXmlReader().LoadDirectory(FixturePath);

        Assert.Empty(package.LoadErrors);
        Assert.Equal("absolute", package.Schema.DateMode);
        Assert.Equal(new[] { "account", "contact", "cmtl_parent", "cmtl_child", "appointment" }, package.Schema.EntityImportOrder);

        var parent = package.Schema.FindEntity("cmtl_parent")!;
        Assert.False(parent.RenderLiquid);
        Assert.False(parent.DisablePlugins);
        Assert.StartsWith("<fetch>", parent.FetchXmlFilter);
        var relationship = Assert.Single(parent.Relationships);
        Assert.True(relationship.IsManyToMany);
        Assert.Equal("cmtl_child", relationship.M2mTargetEntity);

        var child = package.Schema.FindEntity("cmtl_child")!;
        Assert.True(child.FindField("cmtl_name")!.IsUpdateCompare);
        Assert.Equal("relative", child.FindField("cmtl_datetime")!.DateMode);
        Assert.Equal("account|contact|cmtl_parent", child.FindField("cmtl_regardingid")!.LookupType);

        var firstChild = package.Data!.FindEntity("cmtl_child")!.Records[0];
        var file = firstChild.Fields.Single(f => f.Name == "cmtl_file");
        Assert.Equal("sample.txt", file.FileName);
        Assert.Equal("line one\nline two 1", firstChild.Fields.Single(f => f.Name == "cmtl_memo").Value);
        Assert.Equal("[-1,71000010,71000012,-1]", firstChild.Fields.Single(f => f.Name == "cmtl_multichoice").Value);
        var parentLookup = firstChild.Fields.Single(f => f.Name == "cmtl_parentid");
        Assert.Equal("cmtl_parent", parentLookup.LookupEntity);
        Assert.Equal("CMTLAB EX Parent 1", parentLookup.LookupEntityName);
        Assert.Equal(Path.Combine(FixturePath, "data.xml"), firstChild.Source!.FilePath);
        Assert.Equal(31, firstChild.Source.Line);

        var m2m = Assert.Single(package.Data.FindEntity("cmtl_parent")!.ManyToManyRelationships);
        Assert.Equal("cmtl_cmtl_parent_cmtl_child", m2m.RelationshipName);
        Assert.Equal(2, m2m.TargetIds.Count);

        var attendees = package.Data.FindEntity("appointment")!.Records[0].Fields.Single(f => f.Name == "requiredattendees");
        Assert.Equal(2, attendees.ActivityPointerRecords.Count);
        Assert.Equal("account", attendees.ActivityPointerRecords[1].Fields.Single(f => f.Name == "partyid").LookupEntity);
    }

    [Fact]
    public void Fixture_PassesXsd()
    {
        var validator = new SchemaValidator();

        Assert.Empty(validator.ValidateFile(Path.Combine(FixturePath, CmtPackageLayout.SchemaFileName)));
        Assert.Empty(validator.ValidateFile(Path.Combine(FixturePath, CmtPackageLayout.DataFileName)));
    }

    [Theory]
    [InlineData("export")]
    [InlineData("bom")]
    [InlineData("crlf")]
    [InlineData("no-declaration")]
    [InlineData("declaration-without-encoding")]
    [InlineData("root-attributes-over-two-lines")]
    public void Save_UnchangedPackage_KeepsBytes(string variant)
    {
        var schemaBytes = Variant(variant, ReadFixture(CmtPackageLayout.SchemaFileName));
        var dataBytes = Variant(variant, ReadFixture(CmtPackageLayout.DataFileName));
        WritePackage(schemaBytes, dataBytes);
        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        var written = new CmtPackageXmlWriter().Save(package, _root);

        Assert.False(written);
        Assert.Equal(schemaBytes, File.ReadAllBytes(Path.Combine(_root, CmtPackageLayout.SchemaFileName)));
        Assert.Equal(dataBytes, File.ReadAllBytes(Path.Combine(_root, CmtPackageLayout.DataFileName)));
    }

    // Attributes split over lines are the one hand-edited layout XDocument cannot keep once a document changes.
    [Theory]
    [InlineData("export")]
    [InlineData("bom")]
    [InlineData("crlf")]
    [InlineData("no-declaration")]
    [InlineData("declaration-without-encoding")]
    public void Save_EditedValue_ChangesOnlyThatValue(string variant)
    {
        var data = ReadFixture(CmtPackageLayout.DataFileName);
        WritePackage(Variant(variant, ReadFixture(CmtPackageLayout.SchemaFileName)), Variant(variant, data));
        var package = new CmtPackageXmlReader().LoadDirectory(_root);
        package.Data!.FindEntity("cmtl_child")!.Records[0].Set("cmtl_string", "edited");

        var written = new CmtPackageXmlWriter().Save(package, _root);

        Assert.True(written);
        var expected = Variant(variant, data.Replace("value=\"string 1\"", "value=\"edited\""));
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_root, CmtPackageLayout.DataFileName))));
    }

    // The model does not map etc; an existing one is an unmodelled attribute and must survive an edit of its entity.
    [Fact]
    public void Save_EditedSchema_KeepsEtc()
    {
        const string ParentStart = "<entity name=\"cmtl_parent\" ";
        var schema = ReadFixture(CmtPackageLayout.SchemaFileName).Replace(ParentStart, ParentStart + "etc=\"10234\" ");
        WritePackage(Encoding.UTF8.GetBytes(schema), null);
        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        package.Schema.FindEntity("cmtl_parent")!.SkipUpdate = true;
        var written = new CmtPackageXmlWriter().Save(package, _root);

        Assert.True(written);
        Assert.Equal("10234", package.Schema.FindEntity("cmtl_parent")!.OtherAttributes["etc"]);
        var expected = schema.Replace("disableplugins=\"false\">\n    <fields>\n      <field displayname=\"CMTL Parent\"",
            "disableplugins=\"false\" skipupdate=\"true\">\n    <fields>\n      <field displayname=\"CMTL Parent\"");
        Assert.Equal(expected, File.ReadAllText(Path.Combine(_root, CmtPackageLayout.SchemaFileName)));
    }

    [Fact]
    public void Load_MalformedXml_ReportsLoadError()
    {
        WritePackage(Encoding.UTF8.GetBytes("<entities>\n  <entity name=\"account\">\n</entities>"), null);

        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        var error = Assert.Single(package.LoadErrors);
        Assert.Equal(Path.Combine(_root, CmtPackageLayout.SchemaFileName), error.FilePath);
        Assert.Equal(3, error.Line);
        Assert.Empty(package.Schema.Entities);
    }

    // Mapping a bad id to Guid.Empty would make the writer rewrite it and the validators match the wrong record.
    [Fact]
    public void Load_InvalidIds_SkipsThemAndReportsLoadErrors()
    {
        var data = ReadFixture(CmtPackageLayout.DataFileName)
            .Replace("<record id=\"3e05ec78-a3ac-514a-97d0-3a8db43dce8a\">", "<record id=\"not-a-guid\">")
            .Replace("<targetid>ad3c90ed-fb18-5c67-a628-25e89dd1c81a</targetid>", "<targetid>nope</targetid>");
        WritePackage(Encoding.UTF8.GetBytes(ReadFixture(CmtPackageLayout.SchemaFileName)), Encoding.UTF8.GetBytes(data));

        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        Assert.Equal(2, package.LoadErrors.Count);
        Assert.Contains(package.LoadErrors, e => e.Message.Contains("'not-a-guid'") && e.Line == 12);
        Assert.Contains(package.LoadErrors, e => e.Message.Contains("'nope'"));
        var parent = package.Data!.FindEntity("cmtl_parent")!;
        Assert.Single(parent.Records);
        Assert.Single(parent.ManyToManyRelationships[0].TargetIds);
    }

    [Fact]
    public void LoadDirectory_WithoutDataFile_HasNoData()
    {
        WritePackage(Encoding.UTF8.GetBytes(ReadFixture(CmtPackageLayout.SchemaFileName)), null);

        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        Assert.Empty(package.LoadErrors);
        Assert.Null(package.Data);
        Assert.Equal(5, package.Schema.Entities.Count);
    }

    // The variants add their own line endings, so start from LF whatever the checkout produced.
    private static string ReadFixture(string fileName) => File.ReadAllText(Path.Combine(FixturePath, fileName)).Replace("\r\n", "\n");

    private static byte[] Variant(string variant, string text)
    {
        const string Declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
        switch (variant)
        {
            case "bom":
                return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
            case "crlf":
                return Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n"));
            case "no-declaration":
                return Encoding.UTF8.GetBytes(text.Replace(Declaration + "\n", string.Empty));
            case "declaration-without-encoding":
                return Encoding.UTF8.GetBytes(text.Replace(Declaration, "<?xml version=\"1.0\"?>"));
            case "root-attributes-over-two-lines":
                var index = text.IndexOf("<entities ", StringComparison.Ordinal) + "<entities".Length;
                return Encoding.UTF8.GetBytes(text.Substring(0, index) + "\n  " + text.Substring(index + 1));
            default:
                return Encoding.UTF8.GetBytes(text);
        }
    }

    private void WritePackage(byte[] schema, byte[]? data)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, CmtPackageLayout.SchemaFileName), schema);
        if (data != null) File.WriteAllBytes(Path.Combine(_root, CmtPackageLayout.DataFileName), data);
    }
}
