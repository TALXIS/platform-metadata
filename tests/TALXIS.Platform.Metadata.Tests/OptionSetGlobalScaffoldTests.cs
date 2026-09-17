using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class OptionSetGlobalScaffoldTests : IDisposable
{
    private const string OptionSetName = "udpp_paymentmethod";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-optionset-global-scaffold").FullName;
    private readonly string _solutionXmlPath;
    private readonly string _optionSetPath;

    public OptionSetGlobalScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Other"));
        _solutionXmlPath = Path.Combine(_root, "Other", "Solution.xml");
        File.WriteAllText(_solutionXmlPath, """
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents /></SolutionManifest></ImportExportXml>
            """);

        Directory.CreateDirectory(Path.Combine(_root, "OptionSets"));
        _optionSetPath = Path.Combine(_root, "OptionSets", $"{OptionSetName}.xml");
        File.WriteAllText(_optionSetPath, """
            <optionset Name="udpp_paymentmethod" localizedName="Payment Method" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <OptionSetType>picklist</OptionSetType>
              <IsGlobal>1</IsGlobal>
              <IntroducedVersion>1.0</IntroducedVersion>
              <IsCustomizable>1</IsCustomizable>
              <ExternalTypeName></ExternalTypeName>
              <displaynames>
                <displayname description="Payment Method" languagecode="1033" />
              </displaynames>
              <Descriptions>
                <Description description="" languagecode="1033" />
              </Descriptions>
              <options>
              </options>
            </optionset>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_AddsOptionsAndRootComponent()
    {
        OptionSetGlobalScaffold.Apply(new OptionSetGlobalScaffoldRequest
        {
            SolutionRootPath = _root,
            OptionSetName = OptionSetName,
            Options = "{Cash},{Card}",
        });

        var optionSet = XDocument.Load(_optionSetPath);
        var labels = optionSet.Descendants("option")
            .Select(o => o.Descendants("label").First().Attribute("description")?.Value)
            .ToList();
        Assert.Equal(new[] { "Cash", "Card" }, labels);

        var rootComponent = XDocument.Load(_solutionXmlPath).Descendants("RootComponent").Single();
        Assert.Equal("9", rootComponent.Attribute("type")?.Value);
        Assert.Equal(OptionSetName, rootComponent.Attribute("schemaName")?.Value);
    }

    [Fact]
    public void Apply_ExistingRootComponent_IsNotDuplicated()
    {
        File.WriteAllText(_solutionXmlPath, $"""
            <ImportExportXml><SolutionManifest><UniqueName>udpp_Sandbox</UniqueName><RootComponents><RootComponent type="9" schemaName="{OptionSetName}" behavior="0" /></RootComponents></SolutionManifest></ImportExportXml>
            """);

        OptionSetGlobalScaffold.Apply(new OptionSetGlobalScaffoldRequest
        {
            SolutionRootPath = _root,
            OptionSetName = OptionSetName,
            Options = "{Cash}",
        });

        Assert.Single(XDocument.Load(_solutionXmlPath).Descendants("RootComponent"));
    }

    [Fact]
    public void Apply_DoesNotTouchSiblingFiles()
    {
        var rolePath = Path.Combine(_root, "Roles", "Example.xml");
        Directory.CreateDirectory(Path.Combine(_root, "Roles"));
        File.WriteAllText(rolePath, """
            <Role id="{c1b2c3d4-e5f6-4a1b-8c2d-000000000002}" name="Example" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <RolePrivileges>
              </RolePrivileges>
            </Role>
            """);
        var relationshipsPath = Path.Combine(_root, "Other", "Relationships.xml");
        File.WriteAllText(relationshipsPath, """
            <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" />
            """);
        var roleBytes = File.ReadAllBytes(rolePath);
        var relationshipsBytes = File.ReadAllBytes(relationshipsPath);

        OptionSetGlobalScaffold.Apply(new OptionSetGlobalScaffoldRequest
        {
            SolutionRootPath = _root,
            OptionSetName = OptionSetName,
            Options = "{Cash}",
        });

        Assert.Equal(roleBytes, File.ReadAllBytes(rolePath));
        Assert.Equal(relationshipsBytes, File.ReadAllBytes(relationshipsPath));
    }

    [Fact]
    public void Dispatcher_ResolvesOptionSetComponentType()
    {
        ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "OptionSet",
            SolutionRootPath = _root,
            Parameters = new Dictionary<string, string>
            {
                ["optionset-name"] = OptionSetName,
                ["options"] = "{Cash}",
            },
        });

        Assert.Single(XDocument.Load(_optionSetPath).Descendants("option"));
    }
}
