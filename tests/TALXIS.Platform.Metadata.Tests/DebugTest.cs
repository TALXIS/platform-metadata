using System.Xml.Linq;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class DebugTest
{
    [Fact]
    public void DumpErrors()
    {
        var validator = new SchemaValidator();
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
        var results = validator.ValidateXml(doc, "Solution.xml");
        foreach (var r in results)
        {
            Console.WriteLine($"[{r.Severity}] {r.Message}");
        }
        Assert.True(true);
    }
}
