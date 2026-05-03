using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Tests;

public class MergeableNodeTests
{
    [Fact]
    public void SetAttribute_AddsValueAndReadOnlyViewReflectsIt()
    {
        var node = new MergeableNode { Name = "tab" };

        node.SetAttribute("id", "tab-1");

        Assert.Equal("tab-1", node.GetAttribute("id"));
        Assert.Equal("tab-1", node.Attributes["id"]);
    }

    [Fact]
    public void AddChild_AppendsChildAndReadOnlyViewReflectsIt()
    {
        var node = new MergeableNode { Name = "tabs" };
        var child = new MergeableNode { Name = "tab" };

        node.AddChild(child);

        Assert.Single(node.Children);
        Assert.Same(child, node.Children[0]);
    }
}
