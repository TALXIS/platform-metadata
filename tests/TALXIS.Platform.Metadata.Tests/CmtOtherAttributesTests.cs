using System.Xml.Linq;
using TALXIS.Platform.Metadata.ConfigurationMigration;
using TALXIS.Platform.Metadata.ConfigurationMigration.Building;
using TALXIS.Platform.Metadata.ConfigurationMigration.Data;
using TALXIS.Platform.Metadata.ConfigurationMigration.Schema;
using TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

namespace TALXIS.Platform.Metadata.Tests;

public class CmtOtherAttributesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cmt-other-{Guid.NewGuid():N}");

    private const string FilesPackageSchema = """
        <entities >
          <entity name="talxis_file" displayname="File" primaryidfield="talxis_fileid" primarynamefield="talxis_name" renderliquid="false" guidswap="true">
            <fields>
              <field displayname="File" name="talxis_fileid" type="guid" primaryKey="true" updateCompare="true" />
              <field displayname="Name" name="talxis_name" type="string" x-origin="lab" />
            </fields>
          </entity>
        </entities>
        """;

    private const string FilesPackageData = """
        <entities timestamp="2026-10-07T10:00:00.0000000Z">
          <entity name="talxis_file" displayname="File">
            <records>
              <record id="11111111-0000-0000-0000-000000000001" x-batch="7">
                <field name="talxis_fileid" value="11111111-0000-0000-0000-000000000001" />
                <field name="talxis_name" value="Contract" x-note="kept" />
              </record>
            </records>
          </entity>
        </entities>
        """;

    private const string OtherPackageSchema = """
        <entities >
          <entity name="talxis_file" displayname="File" primaryidfield="talxis_fileid" primarynamefield="talxis_name">
            <fields>
              <field displayname="File" name="talxis_fileid" type="guid" primaryKey="true" updateCompare="true" />
            </fields>
          </entity>
        </entities>
        """;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MergedPackageWrittenFromScratch_KeepsAttributesTheModelDoesNotKnow(bool filesPackageFirst)
    {
        var filesPackage = new CmtPackageXmlReader().Read(XDocument.Parse(FilesPackageSchema), XDocument.Parse(FilesPackageData));
        var otherPackage = new CmtPackageXmlReader().Read(XDocument.Parse(OtherPackageSchema), null);
        var packages = filesPackageFirst ? new[] { filesPackage, otherPackage } : new[] { otherPackage, filesPackage };
        var schema = new CmtDataSchema();
        var data = new CmtData();

        foreach (var package in packages)
        {
            foreach (var entity in package.Schema.Entities) CmtSchemaBuilder.MergeEntity(schema, entity);
            foreach (var entity in package.Data?.Entities ?? new List<CmtDataEntity>()) CmtDataBuilder.MergeEntity(data, entity);
        }

        new CmtPackageXmlWriter().Save(new CmtPackage(schema, data), _root);

        var writtenSchema = XDocument.Load(Path.Combine(_root, CmtPackageLayout.SchemaFileName)).Root!;
        var file = writtenSchema.Element("entity")!;
        Assert.Equal("true", (string?)file.Attribute("guidswap"));
        Assert.Equal("lab", (string?)file.Element("fields")!.Elements("field").Single(f => (string?)f.Attribute("name") == "talxis_name").Attribute("x-origin"));
        var record = XDocument.Load(Path.Combine(_root, CmtPackageLayout.DataFileName)).Root!.Element("entity")!.Element("records")!.Element("record")!;
        Assert.Equal("7", (string?)record.Attribute("x-batch"));
        Assert.Equal("kept", (string?)record.Elements("field").Single(f => (string?)f.Attribute("name") == "talxis_name").Attribute("x-note"));
    }

    [Fact]
    public void RemovingAnAttributeTheModelDoesNotKnow_RemovesOnlyThatAttribute()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CmtPackageLayout.SchemaFileName);
        File.WriteAllText(path, FilesPackageSchema);
        var package = new CmtPackageXmlReader().Load(path);

        package.Schema.FindEntity("talxis_file")!.OtherAttributes.Remove("guidswap");
        new CmtPackageXmlWriter().SaveSchema(package, path);

        Assert.Equal(FilesPackageSchema.Replace(" guidswap=\"true\"", string.Empty), File.ReadAllText(path));
    }

    [Fact]
    public void Merge_ReportsAnExtensionAttributeTwoPackagesSetDifferently()
    {
        var first = new CmtPackageXmlReader().Read(XDocument.Parse(FilesPackageSchema), null).Schema.Entities[0];
        var later = new CmtPackageXmlReader().Read(XDocument.Parse(FilesPackageSchema.Replace("guidswap=\"true\"", "guidswap=\"false\"").Replace("x-origin=\"lab\"", "x-origin=\"prod\"")), null).Schema.Entities[0];
        var schema = new CmtDataSchema();
        var warnings = new List<string>();

        CmtSchemaBuilder.MergeEntity(schema, first, warnings);
        CmtSchemaBuilder.MergeEntity(schema, later, warnings);

        Assert.Equal("true", schema.FindEntity("talxis_file")!.OtherAttributes["guidswap"]);
        Assert.Contains(warnings, w => w.Contains("guidswap"));
        Assert.Contains(warnings, w => w.Contains("field 'talxis_name'") && w.Contains("x-origin 'lab' and 'prod'"));
    }
}
