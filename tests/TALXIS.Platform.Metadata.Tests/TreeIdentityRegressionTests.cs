using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml;

namespace TALXIS.Platform.Metadata.Tests;

public class TreeIdentityRegressionTests
{
    [Theory]
    [InlineData("Modified", false)]
    [InlineData("Removed", false)]
    [InlineData("Modified", true)]
    [InlineData("Removed", true)]
    public void MissingId_DoesNotMatchByCountNameOrPosition(string action, bool multiple)
    {
        var source = XDocument.Parse("<tabs><tab id='A' name='general' value='original'/></tabs>");
        if (multiple)
            source.Root!.Add(new XElement("tab", new XAttribute("id", "C")));
        var before = new XDocument(source);
        var layer = XDocument.Parse($"<tabs><tab id='B' name='general' value='changed' solutionaction='{action}'/></tabs>");

        var result = FormXmlMerge.Merge(source, layer);

        Assert.True(XNode.DeepEquals(before, result));
        Assert.True(XNode.DeepEquals(before, source));
    }

    [Theory]
    [InlineData("Modified")]
    [InlineData("Removed")]
    public void MatchingId_ChangesOnlyRequestedSibling(string action)
    {
        var source = XDocument.Parse("<tabs><tab id='A' value='original'/><tab id='B' value='original'/></tabs>");
        var layer = XDocument.Parse($"<tabs><tab id='B' value='changed' solutionaction='{action}'/></tabs>");

        var result = FormXmlMerge.Merge(source, layer);

        Assert.Equal("original", (string?)result.Root!.Elements().Single(e => (string?)e.Attribute("id") == "A").Attribute("value"));
        var target = result.Root.Elements().SingleOrDefault(e => (string?)e.Attribute("id") == "B");
        if (action == "Removed")
            Assert.Null(target);
        else
            Assert.Equal("changed", (string?)target!.Attribute("value"));
    }

    [Fact]
    public void MissingStructuralId_DoesNotApplyNestedChangeToAnotherNode()
    {
        var source = XDocument.Parse("<tabs><tab id='A'><labels><label description='original'/></labels></tab></tabs>");
        var layer = XDocument.Parse("<tabs><tab id='B'><labels><label description='changed' solutionaction='Modified'/></labels></tab></tabs>");

        Assert.True(XNode.DeepEquals(source, FormXmlMerge.Merge(source, layer)));
    }

    [Fact]
    public void CompositeKey_DoesNotFallBackToItsFirstAttribute()
    {
        var source = XDocument.Parse("<events><event name='onload' application='false' enabled='true'/></events>");
        var layer = XDocument.Parse("<events><event name='onload' application='true' solutionaction='Removed'/></events>");

        Assert.True(XNode.DeepEquals(source, FormXmlMerge.Merge(source, layer)));
        var diff = FormXmlMerge.ComputeDiff(source, XDocument.Parse("<events><event name='onload' application='true'/></events>"));
        Assert.Single(diff.Descendants("event"), e => (string?)e.Attribute("solutionaction") == "Added");
        Assert.Single(diff.Descendants("event"), e => (string?)e.Attribute("solutionaction") == "Removed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementId_IsAddedAndRemovedRatherThanModified(bool multiple)
    {
        var source = XDocument.Parse("<tabs><tab id='A' name='general'/></tabs>");
        var modified = XDocument.Parse("<tabs><tab id='B' name='general'/></tabs>");
        if (multiple)
        {
            source.Root!.Add(new XElement("tab", new XAttribute("id", "C")));
            modified.Root!.Add(new XElement("tab", new XAttribute("id", "C")));
        }

        var diff = FormXmlMerge.ComputeDiff(source, modified);

        Assert.Equal("Added", (string?)diff.Descendants("tab").Single(e => (string?)e.Attribute("id") == "B").Attribute("solutionaction"));
        Assert.Equal("Removed", (string?)diff.Descendants("tab").Single(e => (string?)e.Attribute("id") == "A").Attribute("solutionaction"));
        Assert.DoesNotContain(diff.Descendants(), e => (string?)e.Attribute("solutionaction") == "Modified");
        var merged = FormXmlMerge.Merge(source, diff);
        Assert.Equal(modified.Root!.Elements().Select(e => (string?)e.Attribute("id")).Order(), merged.Root!.Elements().Select(e => (string?)e.Attribute("id")).Order());
    }

    [Theory]
    [InlineData("Modified")]
    [InlineData("Removed")]
    public void KeylessRows_PreservePositionalMatching(string action)
    {
        var source = XDocument.Parse("<rows><row value='first'/><row value='second'/></rows>");
        var layer = XDocument.Parse($"<rows><row/><row value='changed' solutionaction='{action}'/></rows>");
        var result = FormXmlMerge.Merge(source, layer);

        Assert.Equal("first", (string?)result.Root!.Elements().First().Attribute("value"));
        if (action == "Removed")
            Assert.Single(result.Root.Elements());
        else
            Assert.Equal("changed", (string?)result.Root.Elements().Last().Attribute("value"));
    }

    [Fact]
    public void SecondaryKey_IsUsedWhenPrimaryKeyIsAbsent()
    {
        var source = XDocument.Parse("<tabs><tab name='general' value='original'/></tabs>");
        var modified = XDocument.Parse("<tabs><tab name='general' value='changed'/></tabs>");
        var diff = FormXmlMerge.ComputeDiff(source, modified);

        Assert.Equal("Modified", (string?)Assert.Single(diff.Descendants("tab")).Attribute("solutionaction"));
        Assert.True(XNode.DeepEquals(modified, FormXmlMerge.Merge(source, diff)));
    }

    [Fact]
    public void AddedId_PreservesExistingSiblingWithSameName()
    {
        var source = XDocument.Parse("<tabs><tab id='A' name='general'/></tabs>");
        var layer = XDocument.Parse("<tabs><tab id='B' name='general' solutionaction='Added'/></tabs>");
        var result = FormXmlMerge.Merge(source, layer);

        Assert.Equal(new[] { "A", "B" }, result.Root!.Elements().Select(e => (string?)e.Attribute("id")));
    }

    [Theory]
    [InlineData("Modified", false)]
    [InlineData("Removed", false)]
    [InlineData("Modified", true)]
    [InlineData("Removed", true)]
    public void WarehouseForm_UsesTabIdentity(string action, bool matchingId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Layering", "warehouse-item-form.xml");
        var bytes = File.ReadAllBytes(path);
        var source = XDocument.Load(path);
        var originalTab = Assert.Single(source.Descendants("tab"));
        var layer = new XDocument(source);
        var target = Assert.Single(layer.Descendants("tab"));
        target.RemoveNodes();
        if (!matchingId)
            target.SetAttributeValue("id", "{11111111-1111-1111-1111-111111111111}");
        target.SetAttributeValue("showlabel", "false");
        target.SetAttributeValue("solutionaction", action);

        var result = FormXmlMerge.Merge(source, layer);

        Assert.Equal((string?)source.Descendants("formid").Single(), (string?)result.Descendants("formid").Single());
        if (!matchingId)
            Assert.True(XNode.DeepEquals(originalTab, Assert.Single(result.Descendants("tab"))));
        else if (action == "Removed")
            Assert.Empty(result.Descendants("tab"));
        else
        {
            var resultTab = Assert.Single(result.Descendants("tab"));
            Assert.Equal("false", (string?)resultTab.Attribute("showlabel"));
            Assert.Equal(originalTab.Descendants("control").Select(e => e.ToString()), resultTab.Descendants("control").Select(e => e.ToString()));
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Null(originalTab.Attribute("solutionaction"));
    }
}
