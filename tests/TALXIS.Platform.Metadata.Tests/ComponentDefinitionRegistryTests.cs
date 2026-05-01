using TALXIS.Platform.Metadata;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentDefinitionRegistryTests
{
    [Theory]
    [InlineData(ComponentType.Entity, 1)]
    [InlineData(ComponentType.Attribute, 2)]
    [InlineData(ComponentType.OptionSet, 9)]
    [InlineData(ComponentType.Role, 20)]
    [InlineData(ComponentType.SavedQuery, 26)]
    [InlineData(ComponentType.Workflow, 29)]
    [InlineData(ComponentType.SystemForm, 60)]
    [InlineData(ComponentType.WebResource, 61)]
    [InlineData(ComponentType.SiteMap, 62)]
    [InlineData(ComponentType.PluginAssembly, 91)]
    [InlineData(ComponentType.SdkMessageProcessingStep, 92)]
    [InlineData(ComponentType.AppModule, 80)]
    [InlineData(ComponentType.CanvasApp, 300)]
    public void CommonTypeCodesHaveCorrectValues(ComponentType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Theory]
    [InlineData(ComponentType.Entity, "Entities", "$(PrimaryName)/Entity.xml", IdentityStrategy.Name)]
    [InlineData(ComponentType.WebResource, "WebResources", "$(PrimaryName)", IdentityStrategy.Guid)]
    [InlineData(ComponentType.SiteMap, "Other", "$(type)$(managed).xml", IdentityStrategy.Singleton)]
    [InlineData(ComponentType.PluginAssembly, "PluginAssemblies", "PluginAssemblies.xml", IdentityStrategy.Guid)]
    [InlineData(ComponentType.Workflow, "Workflows", "Workflows.xml", IdentityStrategy.Guid)]
    [InlineData(ComponentType.Role, "Roles", "$(PrimaryName)", IdentityStrategy.Guid)]
    public void GetByType_ReturnsCorrectDefinition(ComponentType type, string directory, string filePattern, IdentityStrategy identity)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.Equal(type, def.TypeCode);
        Assert.Equal(directory, def.Directory);
        Assert.Equal(filePattern, def.FilePattern);
        Assert.Equal(identity, def.Identity);
    }

    [Theory]
    [InlineData("Entities", ComponentType.Entity)]
    [InlineData("WebResources", ComponentType.WebResource)]
    [InlineData("SiteMap", ComponentType.SiteMap)]
    [InlineData("SolutionPluginAssemblies", ComponentType.PluginAssembly)]
    [InlineData("Roles", ComponentType.Role)]
    public void GetByXmlElement_ReturnsCorrectDefinition(string xmlElement, ComponentType expectedType)
    {
        var def = ComponentDefinitionRegistry.GetByXmlElement(xmlElement);

        Assert.NotNull(def);
        Assert.Equal(expectedType, def.TypeCode);
    }

    [Theory]
    [InlineData("entities")]
    [InlineData("ENTITIES")]
    [InlineData("Entities")]
    public void GetByXmlElement_IsCaseInsensitive(string xmlElement)
    {
        var def = ComponentDefinitionRegistry.GetByXmlElement(xmlElement);

        Assert.NotNull(def);
        Assert.Equal(ComponentType.Entity, def.TypeCode);
    }

    [Fact]
    public void GetByType_UnknownType_ReturnsNull()
    {
        var def = ComponentDefinitionRegistry.GetByType((ComponentType)(-1));

        Assert.Null(def);
    }

    [Fact]
    public void GetByXmlElement_UnknownElement_ReturnsNull()
    {
        var def = ComponentDefinitionRegistry.GetByXmlElement("NonExistentElement");

        Assert.Null(def);
    }

    [Fact]
    public void GetAll_ReturnsNonEmptyCollection()
    {
        var all = ComponentDefinitionRegistry.GetAll().ToList();

        Assert.NotEmpty(all);
        Assert.True(all.Count >= 30, $"Expected at least 30 registered definitions, got {all.Count}");
    }

    [Fact]
    public void Entity_HasSupportsMerge_And_HasSubfolders()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.Entity);

        Assert.NotNull(def);
        Assert.True(def.SupportsMerge);
        Assert.True(def.HasSubfolders);
    }

    [Fact]
    public void WebResource_HasIsFileBacked()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.WebResource);

        Assert.NotNull(def);
        Assert.True(def.IsFileBacked);
    }

    [Fact]
    public void CanvasApp_HasIsFileBacked()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.CanvasApp);

        Assert.NotNull(def);
        Assert.True(def.IsFileBacked);
    }

    [Fact]
    public void SiteMap_HasSingletonIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.SiteMap);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Singleton, def.Identity);
    }

    [Fact]
    public void AppModule_HasSupportsMerge_And_HasSubfolders()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.AppModule);

        Assert.NotNull(def);
        Assert.True(def.SupportsMerge);
        Assert.True(def.HasSubfolders);
    }

    [Fact]
    public void EntityMap_HasCompositeIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.EntityMap);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Composite, def.Identity);
    }

    [Fact]
    public void GenericComponent_HasProbedIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.GenericComponent);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Probed, def.Identity);
    }
}
