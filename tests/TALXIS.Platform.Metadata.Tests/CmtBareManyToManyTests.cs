using System.Text;
using System.Xml.Linq;
using TALXIS.Platform.Metadata.DataMigration;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

/// <summary>
/// CmtData.xsd allows &lt;m2mrelationship&gt; straight under &lt;entity&gt; as well as inside &lt;m2mrelationships&gt;;
/// an association in either shape must be read, and saving must not move or duplicate it.
/// </summary>
public class CmtBareManyToManyTests : IDisposable
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "CmtPackage", "exhaustive");

    private const string Wrapped = """
            <m2mrelationships>
              <m2mrelationship sourceid="11e18694-1be8-5947-8382-580bc409f87b" targetentityname="cmtl_child" targetentitynameidfield="cmtl_childid" m2mrelationshipname="cmtl_cmtl_parent_cmtl_child">
                <targetids>
                  <targetid>e874c01e-7325-549b-aa65-e64722e3e165</targetid>
                  <targetid>ad3c90ed-fb18-5c67-a628-25e89dd1c81a</targetid>
                </targetids>
              </m2mrelationship>
            </m2mrelationships>

        """;

    private const string Bare = """
            <m2mrelationship sourceid="11e18694-1be8-5947-8382-580bc409f87b" targetentityname="cmtl_child" targetentitynameidfield="cmtl_childid" m2mrelationshipname="cmtl_cmtl_parent_cmtl_child">
              <targetids>
                <targetid>e874c01e-7325-549b-aa65-e64722e3e165</targetid>
                <targetid>ad3c90ed-fb18-5c67-a628-25e89dd1c81a</targetid>
              </targetids>
            </m2mrelationship>

        """;

    private const string SecondBare = """
            <m2mrelationship sourceid="3e05ec78-a3ac-514a-97d0-3a8db43dce8a" targetentityname="cmtl_child" targetentitynameidfield="cmtl_childid" m2mrelationshipname="cmtl_cmtl_parent_cmtl_child">
              <targetids>
                <targetid>e874c01e-7325-549b-aa65-e64722e3e165</targetid>
              </targetids>
            </m2mrelationship>

        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cmt-bare-m2m-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Load_BareAssociation_IsRead()
    {
        WritePackage(Data().Replace(Wrapped, Bare));

        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        Assert.Empty(package.LoadErrors);
        var m2m = Assert.Single(package.Data!.FindEntity("cmtl_parent")!.ManyToManyRelationships);
        Assert.Equal("cmtl_cmtl_parent_cmtl_child", m2m.RelationshipName);
        Assert.Equal(2, m2m.TargetIds.Count);
    }

    [Fact]
    public void BareAssociation_PassesXsd()
    {
        WritePackage(Data().Replace(Wrapped, Bare));

        Assert.Empty(new SchemaValidator().ValidateFile(Path.Combine(_root, CmtPackageLayout.DataFileName)));
    }

    [Fact]
    public void Save_UnchangedBareAssociation_KeepsBytes()
    {
        var data = Data().Replace(Wrapped, Bare);
        WritePackage(data);
        var package = new CmtPackageXmlReader().LoadDirectory(_root);

        var written = new CmtPackageXmlWriter().Save(package, _root);

        Assert.False(written);
        Assert.Equal(data, ReadData());
    }

    [Fact]
    public void Save_EditedBareAssociation_StaysBare()
    {
        var data = Data().Replace(Wrapped, Bare);
        WritePackage(data);
        var package = new CmtPackageXmlReader().LoadDirectory(_root);
        package.Data!.FindEntity("cmtl_parent")!.ManyToManyRelationships[0].TargetIds.RemoveAt(1);

        var written = new CmtPackageXmlWriter().Save(package, _root);

        Assert.True(written);
        Assert.Equal(data.Replace("\n        <targetid>ad3c90ed-fb18-5c67-a628-25e89dd1c81a</targetid>", string.Empty), ReadData());
    }

    [Fact]
    public void Save_BothShapesInOneEntity_WritesEveryAssociationOnceInTheWrapper()
    {
        WritePackage(Data().Replace(Wrapped, Wrapped + SecondBare));
        var package = new CmtPackageXmlReader().LoadDirectory(_root);
        Assert.Equal(2, package.Data!.FindEntity("cmtl_parent")!.ManyToManyRelationships.Count);

        new CmtPackageXmlWriter().Save(package, _root);

        var parent = XDocument.Parse(ReadData()).Root!.Elements("entity").Single(e => (string?)e.Attribute("name") == "cmtl_parent");
        Assert.Empty(parent.Elements("m2mrelationship"));
        Assert.Equal(
            new[] { "11e18694-1be8-5947-8382-580bc409f87b", "3e05ec78-a3ac-514a-97d0-3a8db43dce8a" },
            parent.Element("m2mrelationships")!.Elements("m2mrelationship").Select(m => (string)m.Attribute("sourceid")!));
    }

    // Starts from LF whatever the checkout produced, so the replacements above match.
    private static string Data()
    {
        var data = File.ReadAllText(Path.Combine(FixturePath, CmtPackageLayout.DataFileName)).Replace("\r\n", "\n");
        Assert.Contains(Wrapped, data);
        return data;
    }

    private string ReadData() => File.ReadAllText(Path.Combine(_root, CmtPackageLayout.DataFileName));

    private void WritePackage(string data)
    {
        Directory.CreateDirectory(_root);
        File.Copy(Path.Combine(FixturePath, CmtPackageLayout.SchemaFileName), Path.Combine(_root, CmtPackageLayout.SchemaFileName));
        File.WriteAllBytes(Path.Combine(_root, CmtPackageLayout.DataFileName), Encoding.UTF8.GetBytes(data));
    }
}
