using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionLayeringTests
{
    [Fact]
    public void LayerStack_TopWins()
    {
        var stack = new LayerStack { ComponentType = ComponentType.Entity, ComponentId = "account" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "ISV", Order = 1, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "Active", Order = 2, IsManaged = false });

        Assert.Equal(3, stack.Layers.Count);
        Assert.Equal("Active", stack.ActiveLayer!.SolutionName);
        Assert.Equal("Base", stack.BaseLayer!.SolutionName);
    }

    [Fact]
    public void LayerStack_RemoveLayer()
    {
        var stack = new LayerStack { ComponentType = ComponentType.Entity, ComponentId = "contact" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true });
        stack.PushLayer(new ComponentLayer { SolutionName = "ISV", Order = 1, IsManaged = true });

        var removed = stack.RemoveLayer("ISV");

        Assert.True(removed);
        Assert.Single(stack.Layers);
        Assert.Equal("Base", stack.Layers[0].SolutionName);
        Assert.False(stack.RemoveLayer("NonExistent"));
    }

    [Fact]
    public void LayerStack_ResolveTopWins_ReturnsTopContent()
    {
        var stack = new LayerStack { ComponentType = ComponentType.SystemForm, ComponentId = "form-1" };
        stack.PushLayer(new ComponentLayer { SolutionName = "Base", Order = 0, IsManaged = true, XmlContent = "<form>base</form>" });
        stack.PushLayer(new ComponentLayer { SolutionName = "Active", Order = 1, IsManaged = false, XmlContent = "<form>custom</form>" });

        Assert.Equal("<form>custom</form>", stack.ResolveTopWins());
    }

    [Fact]
    public void LayerStack_ResolveTopWins_ReturnsNull_WhenTopDeleted()
    {
        var stack = new LayerStack { ComponentType = ComponentType.WebResource, ComponentId = "wr-1" };
        stack.PushLayer(new ComponentLayer
        {
            SolutionName = "Active", Order = 0, XmlContent = "<wr/>",
            State = ComponentState.Deleted
        });

        Assert.Null(stack.ResolveTopWins());
    }

    [Fact]
    public void SolutionLayerManager_ImportAndFind()
    {
        var mgr = new SolutionLayerManager();
        var components = new (ComponentType, string, string?)[]
        {
            (ComponentType.Entity, "account", "<entity>account</entity>"),
            (ComponentType.SystemForm, "form-1", "<form>main</form>")
        };

        mgr.ImportSolutionLayer("MySolution", 1, true, components);

        var entityStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(entityStack);
        Assert.Single(entityStack!.Layers);
        Assert.Equal("MySolution", entityStack.Layers[0].SolutionName);
        Assert.True(entityStack.Layers[0].IsManaged);
        Assert.Equal("<entity>account</entity>", entityStack.Layers[0].XmlContent);

        var formStack = mgr.FindStack(ComponentType.SystemForm, "form-1");
        Assert.NotNull(formStack);
        Assert.Equal("<form>main</form>", formStack!.Layers[0].XmlContent);

        Assert.Null(mgr.FindStack(ComponentType.Entity, "nonexistent"));
    }

    [Fact]
    public void SolutionLayerManager_RemoveSolution()
    {
        var mgr = new SolutionLayerManager();

        mgr.ImportSolutionLayer("Sol1", 0, true, new[]
        {
            (ComponentType.Entity, "account", (string?)"<e>sol1</e>"),
            (ComponentType.Attribute, "name", (string?)"<a>sol1</a>")
        });
        mgr.ImportSolutionLayer("Sol2", 1, true, new[]
        {
            (ComponentType.Entity, "account", (string?)"<e>sol2</e>")
        });

        mgr.RemoveSolutionLayers("Sol1");

        // account stack still exists (has Sol2 layer)
        var accountStack = mgr.FindStack(ComponentType.Entity, "account");
        Assert.NotNull(accountStack);
        Assert.Single(accountStack!.Layers);
        Assert.Equal("Sol2", accountStack.Layers[0].SolutionName);

        // name stack was emptied and cleaned up
        Assert.Null(mgr.FindStack(ComponentType.Attribute, "name"));
        Assert.Single(mgr.AllStacks);
    }

    [Fact]
    public void SolutionLayerManager_GetOrCreate_ReturnsSameInstance()
    {
        var mgr = new SolutionLayerManager();

        var stack1 = mgr.GetOrCreateStack(ComponentType.Entity, "account");
        var stack2 = mgr.GetOrCreateStack(ComponentType.Entity, "account");

        Assert.Same(stack1, stack2);
    }
}
