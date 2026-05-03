using TALXIS.Platform.Metadata;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentDefinitionTests
{
    [Theory]
    [InlineData(ComponentType.Form)]
    [InlineData(ComponentType.SystemForm)]
    [InlineData(ComponentType.SiteMap)]
    public void MergeableComponents_HaveIsMergeableTrue(ComponentType type)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.True(def.IsMergeable, $"{type} should be mergeable");
    }

    [Theory]
    [InlineData(ComponentType.Entity)]
    [InlineData(ComponentType.OptionSet)]
    [InlineData(ComponentType.Role)]
    [InlineData(ComponentType.AppModule)]
    public void NonMergeableComponents_HaveIsMergeableFalse(ComponentType type)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.False(def.IsMergeable, $"{type} should not be mergeable");
    }

    [Fact]
    public void Attribute_HasParent_And_RootComponentIsEntity()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.Attribute);

        Assert.NotNull(def);
        Assert.True(def.HasParent);
        Assert.Equal((int)ComponentType.Entity, def.RootComponent);
    }

    [Fact]
    public void SdkMessageProcessingStep_RootComponentIsPluginAssembly()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.SdkMessageProcessingStep);

        Assert.NotNull(def);
        Assert.True(def.HasParent);
        Assert.Equal((int)ComponentType.PluginAssembly, def.RootComponent);
    }

    [Theory]
    [InlineData(ComponentType.Entity)]
    [InlineData(ComponentType.OptionSet)]
    [InlineData(ComponentType.Role)]
    [InlineData(ComponentType.PluginAssembly)]
    [InlineData(ComponentType.WebResource)]
    [InlineData(ComponentType.Workflow)]
    [InlineData(ComponentType.AppModule)]
    [InlineData(ComponentType.SiteMap)]
    [InlineData(ComponentType.CustomControl)]
    [InlineData(ComponentType.Connector)]
    public void RootComponents_HaveRootComponentZero(ComponentType type)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.False(def.HasParent);
        Assert.Equal(0, def.RootComponent);
    }

    [Theory]
    [InlineData(ComponentType.Attribute, 1)]
    [InlineData(ComponentType.Form, 1)]
    [InlineData(ComponentType.SystemForm, 1)]
    [InlineData(ComponentType.SavedQuery, 1)]
    [InlineData(ComponentType.SdkMessageProcessingStep, 91)]
    public void ChildComponents_HaveCorrectRootComponent(ComponentType type, int expectedRoot)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.True(def.HasParent);
        Assert.Equal(expectedRoot, def.RootComponent);
    }

    [Fact]
    public void NewProperties_DefaultToSafeValues()
    {
        // A component registered without new properties should have safe defaults
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.EntityRelationship);

        Assert.NotNull(def);
        Assert.False(def.IsMergeable);
        Assert.False(def.HasParent);
        Assert.Equal(0, def.RootComponent);
        Assert.Null(def.SerializedPath);
        Assert.Null(def.PrimaryKeyName);
    }
}
