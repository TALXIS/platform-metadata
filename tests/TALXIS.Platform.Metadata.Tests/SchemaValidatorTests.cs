using System.Xml.Linq;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _validator = new();

    // Entity.xsd requires <Name OriginalName="..." LocalizedName="...">value</Name> (minOccurs=1).
    // EntityInfo and everything else is optional.
    private const string ValidEntityXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <Name OriginalName="test_entity" LocalizedName="Test Entity">test_entity</Name>
          <EntityInfo>
            <entity Name="test_entity">
              <LocalizedNames>
                <LocalizedName description="Test" languagecode="1033" />
              </LocalizedNames>
              <LocalizedCollectionNames>
                <LocalizedCollectionName description="Tests" languagecode="1033" />
              </LocalizedCollectionNames>
              <Descriptions>
                <Description description="" languagecode="1033" />
              </Descriptions>
              <attributes />
              <EntitySetName>test_entities</EntitySetName>
            </entity>
          </EntityInfo>
        </Entity>
        """;

    private const string ValidSolutionXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ImportExportXml version="9.2" SolutionPackageVersion="9.2" languagecode="1033">
          <SolutionManifest>
            <UniqueName>TestSolution</UniqueName>
            <LocalizedNames>
              <LocalizedName description="Test Solution" languagecode="1033" />
            </LocalizedNames>
            <Descriptions></Descriptions>
            <Version>1.0.0.0</Version>
            <Managed>0</Managed>
            <Publisher>
              <UniqueName>test</UniqueName>
              <LocalizedNames>
                <LocalizedName description="Test Publisher" languagecode="1033" />
              </LocalizedNames>
              <Descriptions>
                <Description description="" languagecode="1033" />
              </Descriptions>
              <EMailAddress />
              <SupportingWebsiteUrl />
              <CustomizationPrefix>test</CustomizationPrefix>
              <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
              <Addresses>
                <Address>
                  <AddressNumber>1</AddressNumber>
                  <AddressTypeCode>1</AddressTypeCode>
                </Address>
              </Addresses>
            </Publisher>
            <RootComponents />
            <MissingDependencies />
          </SolutionManifest>
        </ImportExportXml>
        """;

    [Fact]
    public void ValidEntityXml_PassesValidation()
    {
        var doc = XDocument.Parse(ValidEntityXml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        Assert.Empty(results);
    }

    [Fact]
    public void InvalidEntityXml_MissingRequiredElement_FailsValidation()
    {
        // Missing required <Name> element
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <EntityInfo>
                <entity Name="test_entity">
                  <attributes />
                </entity>
              </EntityInfo>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Entity.xml");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidationResult_ContainsFilePathAndMessage()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <UnknownElement>bad</UnknownElement>
            </Entity>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "src/Entities/Entity.xml");

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal("src/Entities/Entity.xml", first.FilePath);
        Assert.False(string.IsNullOrWhiteSpace(first.Message));
    }

    [Fact]
    public void MultipleErrors_AllCollected()
    {
        // Solution.xsd uses xs:sequence — wrong element order and wrong types
        // produce separate validation events
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml>
              <SolutionManifest>
                <UniqueName>!!!INVALID!!!</UniqueName>
                <LocalizedNames />
                <Descriptions />
                <Version>1.0</Version>
                <Managed>999</Managed>
                <Publisher>
                  <UniqueName>!!!ALSO INVALID!!!</UniqueName>
                  <LocalizedNames />
                  <Descriptions />
                  <EMailAddress />
                  <SupportingWebsiteUrl />
                  <CustomizationPrefix>x</CustomizationPrefix>
                  <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
                  <Addresses />
                </Publisher>
                <RootComponents />
              </SolutionManifest>
            </ImportExportXml>
            """;

        var doc = XDocument.Parse(xml);
        var results = _validator.ValidateXml(doc, "Solution.xml");

        Assert.True(results.Count > 1, $"Expected multiple errors but got {results.Count}: {string.Join("; ", results.Select(r => r.Message))}");
    }

    [Fact]
    public void ValidSolutionXml_PassesValidation()
    {
        var doc = XDocument.Parse(ValidSolutionXml);
        var results = _validator.ValidateXml(doc, "Solution.xml");

        Assert.Empty(results);
    }
}
