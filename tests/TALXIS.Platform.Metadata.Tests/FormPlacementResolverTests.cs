using System.Xml;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormPlacementResolverTests
{
    private static XmlDocument FormDoc()
    {
        var doc = new XmlDocument();
        doc.LoadXml("""
            <form>
              <tabs>
                <tab id="{00000000-0000-0000-0000-0000000000a1}">
                  <tabfooter />
                  <columns>
                    <column>
                      <sections>
                        <section id="{00000000-0000-0000-0000-0000000000b1}">
                          <rows>
                            <row name="first" />
                            <row name="second" />
                          </rows>
                        </section>
                        <section id="{00000000-0000-0000-0000-0000000000b2}" />
                      </sections>
                    </column>
                  </columns>
                </tab>
                <tab id="{00000000-0000-0000-0000-0000000000a2}">
                  <columns>
                    <column>
                      <sections>
                        <section id="{00000000-0000-0000-0000-0000000000c1}" />
                      </sections>
                    </column>
                  </columns>
                </tab>
              </tabs>
            </form>
            """);
        return doc;
    }

    private static string SectionId(XmlNode section) => ((XmlElement)section).GetAttribute("id");

    [Fact]
    public void Resolve_Defaults_UsesLastTabLastColumnLastSection()
    {
        var section = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement());
        Assert.Equal("{00000000-0000-0000-0000-0000000000c1}", SectionId(section));
    }

    [Fact]
    public void Resolve_ByTabAndSectionId_ReturnsThatSection()
    {
        var section = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement
        {
            TabId = "00000000-0000-0000-0000-0000000000a1",
            SectionId = "00000000-0000-0000-0000-0000000000b1",
        });
        Assert.Equal("{00000000-0000-0000-0000-0000000000b1}", SectionId(section));
    }

    [Fact]
    public void Resolve_UnmatchedTabId_FallsBackToTabIndex()
    {
        var section = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement
        {
            TabId = "00000000-0000-0000-0000-0000000000ff",
            TabIndex = "1",
            SectionIndex = "2",
        });
        Assert.Equal("{00000000-0000-0000-0000-0000000000b2}", SectionId(section));
    }

    [Fact]
    public void Resolve_UnmatchedTabIdWithoutIndex_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement
        {
            TabId = "00000000-0000-0000-0000-0000000000ff",
        }));
    }

    [Fact]
    public void Resolve_TabFooter_ReturnsFooterNode()
    {
        var footer = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement
        {
            TabIndex = "1",
            SetToTabFooter = true,
        });
        Assert.Equal("tabfooter", footer.Name);
    }

    [Fact]
    public void Resolve_TabFooterMissing_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement
        {
            TabIndex = "2",
            SetToTabFooter = true,
        }));
    }

    [Fact]
    public void ResolveTargetRow_DefaultsToLastRow()
    {
        var section = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement { TabIndex = "1", SectionIndex = "1" });
        var row = FormPlacementResolver.ResolveTargetRow(section, rowIndex: null);
        Assert.Equal("second", ((XmlElement)row).GetAttribute("name"));
    }

    [Fact]
    public void ResolveTargetRow_ByIndex_ReturnsThatRow()
    {
        var section = FormPlacementResolver.ResolveTargetSection(FormDoc(), new FormPlacement { TabIndex = "1", SectionIndex = "1" });
        var row = FormPlacementResolver.ResolveTargetRow(section, rowIndex: "1");
        Assert.Equal("first", ((XmlElement)row).GetAttribute("name"));
    }
}
