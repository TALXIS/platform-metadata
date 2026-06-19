using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class SolutionTests
{
    [Fact]
    public void Create_WithRequiredProperties_SetsValues()
    {
        var solution = new Solution { UniqueName = "MySolution" };

        Assert.Equal("MySolution", solution.UniqueName);
        Assert.Equal("1.0.0.0", solution.Version);
        Assert.False(solution.IsManaged);
        Assert.Equal("0", solution.ManagedValue);
        Assert.Null(solution.Publisher);
        Assert.NotNull(solution.DisplayName);
        Assert.NotNull(solution.Description);
        Assert.Empty(solution.RootComponents);
    }

    [Fact]
    public void ManagedValue_NonZeroValue_IsManagedAndPreserved()
    {
        var solution = new Solution { UniqueName = "MySolution", ManagedValue = "2" };

        Assert.True(solution.IsManaged);
        Assert.Equal("2", solution.ManagedValue);
    }

    [Fact]
    public void IsManaged_Setter_UpdatesManagedValue()
    {
        var solution = new Solution { UniqueName = "MySolution" };

        solution.IsManaged = true;
        Assert.Equal("1", solution.ManagedValue);

        solution.IsManaged = false;
        Assert.Equal("0", solution.ManagedValue);
    }

    [Fact]
    public void AddRootComponent_AddsToCollection()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        var component = new RootComponent { Type = ComponentType.Entity, SchemaName = "account" };

        solution.AddRootComponent(component);

        Assert.Single(solution.RootComponents);
        Assert.Same(component, solution.RootComponents[0]);
    }

    [Fact]
    public void AddRootComponent_MultipleComponents_AllPresent()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "account" });
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "contact" });
        solution.AddRootComponent(new RootComponent { Type = ComponentType.SavedQuery, SchemaName = "activeaccounts" });

        Assert.Equal(3, solution.RootComponents.Count);
    }

    [Fact]
    public void RemoveRootComponent_ByTypeAndSchemaName_RemovesCorrectComponent()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "account" });
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "contact" });

        solution.RemoveRootComponent(ComponentType.Entity, "account");

        Assert.Single(solution.RootComponents);
        Assert.Equal("contact", solution.RootComponents[0].SchemaName);
    }

    [Fact]
    public void RemoveRootComponent_CaseInsensitive()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "Account" });

        solution.RemoveRootComponent(ComponentType.Entity, "account");

        Assert.Empty(solution.RootComponents);
    }

    [Fact]
    public void RemoveRootComponent_NonExistent_DoesNothing()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "account" });

        solution.RemoveRootComponent(ComponentType.Entity, "nonexistent");

        Assert.Single(solution.RootComponents);
    }

    [Fact]
    public void FindRootComponent_ReturnsCorrectComponent()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        var target = new RootComponent { Type = ComponentType.SavedQuery, SchemaName = "activeaccounts" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "account" });
        solution.AddRootComponent(target);

        var found = solution.FindRootComponent(ComponentType.SavedQuery, "activeaccounts");

        Assert.Same(target, found);
    }

    [Fact]
    public void FindRootComponent_CaseInsensitive()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        var component = new RootComponent { Type = ComponentType.Entity, SchemaName = "Account" };
        solution.AddRootComponent(component);

        Assert.Same(component, solution.FindRootComponent(ComponentType.Entity, "account"));
        Assert.Same(component, solution.FindRootComponent(ComponentType.Entity, "ACCOUNT"));
    }

    [Fact]
    public void FindRootComponent_UnknownComponent_ReturnsNull()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "account" });

        Assert.Null(solution.FindRootComponent(ComponentType.Entity, "nonexistent"));
        Assert.Null(solution.FindRootComponent(ComponentType.PluginAssembly, "account"));
    }

    [Fact]
    public void Publisher_Properties_CanBeSetAndRead()
    {
        var publisher = new Publisher
        {
            UniqueName = "contoso",
            Prefix = "con",
            OptionValuePrefix = 10000
        };
        publisher.DisplayName[1033] = "Contoso";

        Assert.Equal("contoso", publisher.UniqueName);
        Assert.Equal("con", publisher.Prefix);
        Assert.Equal(10000, publisher.OptionValuePrefix);
        Assert.Equal("Contoso", publisher.DisplayName[1033]);
    }

    [Fact]
    public void Solution_WithPublisher_CanBeAccessed()
    {
        var publisher = new Publisher { UniqueName = "contoso", Prefix = "con" };
        var solution = new Solution { UniqueName = "MySolution", Publisher = publisher };

        Assert.NotNull(solution.Publisher);
        Assert.Equal("contoso", solution.Publisher.UniqueName);
        Assert.Equal("con", solution.Publisher.Prefix);
    }

    [Fact]
    public void RootComponent_WithGuid_CanBeAccessed()
    {
        var id = Guid.NewGuid();
        var component = new RootComponent { Type = ComponentType.WebResource, Id = id, Behavior = 1 };

        Assert.Equal(ComponentType.WebResource, component.Type);
        Assert.Equal(id, component.Id);
        Assert.Equal(1, component.Behavior);
        Assert.Equal(RootComponentBehavior.DoNotIncludeSubcomponents, component.BehaviorOption);
    }

    [Fact]
    public void RootComponent_BehaviorOption_SynchronizesRawBehavior()
    {
        var component = new RootComponent { Type = ComponentType.Entity };

        component.BehaviorOption = RootComponentBehavior.DoNotIncludeSubcomponents;

        Assert.Equal(1, component.Behavior);
        Assert.Equal(RootComponentBehavior.DoNotIncludeSubcomponents, component.BehaviorOption);
    }

    [Fact]
    public void FindRootComponent_WithNullSchemaName()
    {
        var solution = new Solution { UniqueName = "MySolution" };
        var component = new RootComponent { Type = ComponentType.SiteMap, SchemaName = null };
        solution.AddRootComponent(component);

        var found = solution.FindRootComponent(ComponentType.SiteMap, null);

        Assert.Same(component, found);
    }
}
