using System.Xml.Linq;
using TALXIS.Platform.Metadata.Merging;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class MergeableNodeXmlConverterTests
{
    [Fact]
    public void Roundtrip_XElement_To_MergeableNode_And_Back()
    {
        var xml = XElement.Parse(@"
<form>
  <tabs>
    <tab id=""tab1"" name=""General"">
      <sections>
        <section id=""s1"" name=""Details"">
          <rows>
            <row>
              <cell id=""c1"">
                <control id=""name"" datafieldname=""fullname"" />
              </cell>
            </row>
          </rows>
        </section>
      </sections>
    </tab>
  </tabs>
</form>");

        var node = MergeableNodeXmlConverter.FromXElement(xml);

        Assert.Equal("form", node.Name);
        Assert.Single(node.Children); // tabs
        var tab = node.Children[0].Children[0]; // tabs -> tab
        Assert.Equal("tab1", tab.GetAttribute("id"));
        Assert.Equal("General", tab.GetAttribute("name"));

        var backToXml = MergeableNodeXmlConverter.ToXElement(node);
        Assert.Equal("form", backToXml.Name.LocalName);
        Assert.Equal("tab1", backToXml.Descendants("tab").First().Attribute("id")?.Value);
    }

    [Fact]
    public void SolutionAction_Maps_To_MergeAction()
    {
        var xml = XElement.Parse(@"<tab id=""t1"" solutionaction=""Added"" />");
        var node = MergeableNodeXmlConverter.FromXElement(xml);

        Assert.Equal(MergeAction.Added, node.Action);
        Assert.Null(node.GetAttribute("solutionaction")); // not stored as attribute
    }

    [Fact]
    public void MergeAction_Maps_To_SolutionAction()
    {
        var node = new MergeableNode { Name = "section", Action = MergeAction.Removed };
        node.SetAttribute("id", "s1");

        var xml = MergeableNodeXmlConverter.ToXElement(node);

        Assert.Equal("Removed", xml.Attribute("solutionaction")?.Value);
        Assert.Equal("s1", xml.Attribute("id")?.Value);
    }

    [Fact]
    public void TextContent_Preserved()
    {
        var xml = XElement.Parse(@"<label>Hello World</label>");
        var node = MergeableNodeXmlConverter.FromXElement(xml);

        Assert.Equal("Hello World", node.TextContent);

        var backToXml = MergeableNodeXmlConverter.ToXElement(node);
        Assert.Equal("Hello World", backToXml.Value);
    }
}
