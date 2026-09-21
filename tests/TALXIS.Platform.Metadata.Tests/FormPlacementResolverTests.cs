using System.Xml;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormPlacementResolverTests
{
    private static XmlDocument FormWithTwoRows()
    {
        var doc = new XmlDocument();
        doc.LoadXml("""
            <form>
              <tabs>
                <tab id="{a1000000-0000-4000-8000-000000000001}">
                  <columns>
                    <column>
                      <sections>
                        <section>
                          <rows><row /><row /></rows>
                        </section>
                      </sections>
                    </column>
                  </columns>
                </tab>
              </tabs>
            </form>
            """);
        return doc;
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ResolveTab_NonPositiveIndex_IsNotFound(string index)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            FormPlacementResolver.ResolveTab(FormWithTwoRows(), new FormPlacement { TabIndex = index }));
        Assert.Contains("1-based", ex.Message);
    }

    [Fact]
    public void ResolveTargetRow_IndexZero_DoesNotWrapToLastRow()
    {
        var doc = FormWithTwoRows();
        var section = doc.SelectSingleNode("//section")!;

        Assert.Throws<InvalidOperationException>(() =>
            FormPlacementResolver.ResolveTargetRow(section, "0"));
    }

    [Fact]
    public void ResolveTargetRow_ValidIndex_ReturnsFirstRow()
    {
        var doc = FormWithTwoRows();
        var section = doc.SelectSingleNode("//section")!;

        var row = FormPlacementResolver.ResolveTargetRow(section, "1");

        Assert.Same(doc.SelectNodes("//row")![0], row);
    }
}
