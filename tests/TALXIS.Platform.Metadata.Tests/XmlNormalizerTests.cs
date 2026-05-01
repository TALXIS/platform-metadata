using System.Xml.Linq;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class XmlNormalizerTests
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    [Fact]
    public void NilElement_WithWhitespaceContent_IsStripped()
    {
        var doc = XDocument.Parse("""
            <Root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Field xsi:nil="true">
              </Field>
            </Root>
            """);

        XmlNormalizer.NormalizeNilElements(doc);

        var field = doc.Root!.Element("Field")!;
        Assert.Empty(field.Nodes());
        Assert.Equal("true", field.Attribute(Xsi + "nil")!.Value);
    }

    [Fact]
    public void NilElement_WithNoContent_IsUnchanged()
    {
        var doc = XDocument.Parse("""
            <Root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Field xsi:nil="true" />
            </Root>
            """);

        XmlNormalizer.NormalizeNilElements(doc);

        var field = doc.Root!.Element("Field")!;
        Assert.Empty(field.Nodes());
        Assert.Equal("true", field.Attribute(Xsi + "nil")!.Value);
    }

    [Fact]
    public void NonNilElements_AreUnaffected()
    {
        var doc = XDocument.Parse("""
            <Root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Field>some value</Field>
              <Other>
                <Child>nested</Child>
              </Other>
            </Root>
            """);

        XmlNormalizer.NormalizeNilElements(doc);

        Assert.Equal("some value", doc.Root!.Element("Field")!.Value);
        Assert.Equal("nested", doc.Root!.Element("Other")!.Element("Child")!.Value);
    }

    [Fact]
    public void MultipleNilElements_AllNormalized()
    {
        var doc = XDocument.Parse("""
            <Root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <A xsi:nil="true">  whitespace  </A>
              <B>keep this</B>
              <C xsi:nil="true">
                extra content
              </C>
            </Root>
            """);

        XmlNormalizer.NormalizeNilElements(doc);

        Assert.Empty(doc.Root!.Element("A")!.Nodes());
        Assert.Equal("keep this", doc.Root!.Element("B")!.Value);
        Assert.Empty(doc.Root!.Element("C")!.Nodes());
    }
}
